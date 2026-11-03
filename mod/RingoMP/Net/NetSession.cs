using dc;
using dc.cine;
using dc.en;
using dc.pr;
using ModCore.Utilities;
using Serilog;

namespace RingoMP.Net
{
    public enum NetRole { Host, Client }

    /// Keeps up to four games in step. The host owns mobs: where they are, their life
    /// and their death. Each player owns their own hero. The others' mobs are puppets,
    /// and their hits on them only show; the real ones come from their RemoteHeroes
    /// replaying the same inputs on the host.
    /// Every player links to the host alone, and the host passes on what each one says
    /// about themselves to the others, so that everyone sees everyone.
    /// A session lives as long as the game does. Alone it just watches, so a friend
    /// can join at any moment: same seed, same levels, straight to where the host is.
    public sealed class NetSession
    {
        public const int MaxPlayers = 4;
        const double HeroSendSeconds = 1.0 / 30;
        const double MobSendSeconds = 1.0 / 15;
        const double PuppetAiLockSeconds = 100000;
        const double MobLifeScale = 1.5;
        const double EliteLifeScale = 1.7;
        const double BossLifeScale = 1.8;
        const int MobSyncRangeCells = 60;
        const double RetireSeconds = 1;

        /// Another player, as this game knows them.
        sealed class Peer(int slot)
        {
            public readonly int Slot = slot;
            public (byte Number, byte Room, string Id)? Arrival;
            public readonly Dictionary<(byte Number, byte Room), ulong> Signatures = [];
            public readonly Dictionary<byte, string> Levels = [];
            public ulong SteamId;
            public bool Up;
            public bool EverUp;
            public double DownSeconds;
            public bool ContentsSent;
        }

        // What a player says about themselves, which the host passes on to the others.
        static readonly HashSet<PacketType> Relayed =
        [
            PacketType.HeroState, PacketType.LevelReady, PacketType.ExitWait, PacketType.Rescue, PacketType.TeamWipe,
            PacketType.ItemTaken, PacketType.ItemPlaced, PacketType.Look, PacketType.Progress, PacketType.LoadoutSaved,
        ];

        // Said once and true until said again, so a player who joins later hears it from the host.
        static readonly HashSet<PacketType> Remembered =
        [
            PacketType.LevelReady, PacketType.Look, PacketType.Progress, PacketType.LoadoutSaved,
        ];

        readonly CoopSession coop;
        readonly ILogger log;
        readonly Dictionary<Mob, bool> dead = new(ReferenceEqualityComparer.Instance);
        readonly Dictionary<ushort, string> lastAnims = [];
        readonly Dictionary<byte, string> runLevels = [];
        readonly SortedDictionary<int, Peer> peers = [];
        readonly Dictionary<(int Slot, PacketType Type), byte[]> remembered = [];
        // Links of players who left or were sent away, ignored until they close.
        readonly HashSet<int> dropped = [];
        readonly Dictionary<ulong, byte[]> savedLoadouts = [];
        // How much stronger each mob was made for the players there were at the time.
        readonly Dictionary<Mob, double> mobScales = new(ReferenceEqualityComparer.Instance);
        ITransport transport;
        // A link that was left still runs a moment, so that the goodbye goes out.
        ITransport? retiring;
        double retiringFor;
        Game? trackedGame;
        bool routeFound;
        double heroTimer;
        double mobTimer;
        Level? trackedLevel;
        Game? runGame;
        byte levelNumber;
        // Side rooms (the bank, challenge rooms and the like) are levels of their own
        // within a level. The main one is room 0, and the others keep their mobs while
        // the players are out, as the game keeps the rooms themselves.
        byte room;
        Level? roomsMain;
        readonly HashSet<Level> roomsSeen = new(ReferenceEqualityComparer.Instance);
        readonly Dictionary<Level, MobRegistry> roomRegistries = new(ReferenceEqualityComparer.Instance);
        int runsStarted;
        bool signatureSent;
        bool allowDeath;
        bool itemsTracked;
        bool everLinked;
        bool launching;
        bool hostInRun;
        bool saidWaiting;
        int? seedTold;
        bool? runTold;
        RunMode? modeTold;
        int? startedSeed;
        RunMode hostMode;
        bool savingScore;
        int ownSlot;
        double joinWait;
        byte[]? loadoutToRestore;

        public NetRole Role { get; private set; }

        /// How long a lost player has to come back before they count as gone.
        public double GiveUpSeconds { get; set; } = 60;

        /// A link went down and may yet come back.
        public bool LinkDown => everLinked && !PeerLeft && peers.Values.Any(p => Direct(p) && !p.Up);

        /// Everyone else is gone for good and this player goes on alone.
        public bool PeerLeft { get; private set; }

        /// Players start runs from the title screen rather than a test: whatever the
        /// host starts, the others join, and they cannot start one by themselves.
        public bool HostLeadsStart { get; init; }

        /// Says why an attempt to start was refused.
        public Action<Phrase>? Refused { get; set; }

        /// This player was kicked out of the room.
        public Action? KickedOut { get; set; }

        public ITransport Transport => transport;
        public int? Seed { get; private set; }
        public MobRegistry? Registry { get; private set; }

        /// A player who joined matches the host; the host matches everyone.
        public bool LevelsMatch => Role == NetRole.Client
            ? peers.TryGetValue(SteamTransport.HostLink, out var host) && Matches(host)
            : peers.Count > 0 && peers.Values.All(Matches);

        /// Which player this is: the host is 0, the others are numbered as they joined.
        public int Slot => Role == NetRole.Host ? SteamTransport.HostLink : ownSlot;

        /// Everyone in the session, this player included.
        public int Players => 1 + peers.Count;

        /// In a side room rather than the level itself, for tests to follow along.
        public bool InSideRoom => room != 0;
        public bool Connected => transport.Connected;
        public bool InRun => Game.Class.ME?.hero != null;
        public string? LevelId => Game.Class.ME is { curLevel: not null } game ? CurrentLevelId(game) : null;
        public ExitGathering Gathering { get; }
        public TeamLife Life { get; }
        public SharedItems Items { get; }
        public SessionProgress Progress { get; }
        public PartnerLook Look { get; }

        /// The kind of run the host is in or last started, which the others start too.
        public RunMode Mode => hostMode;

        /// Someone else has been part of this session since the last run began alone.
        public bool PlayedTogether => everLinked;

        /// A run can start: the seed and everyone's progress are known.
        public bool ReadyToStart => Seed != null && Progress.Ready;

        /// For tests: games sharing one install must not load their saves at the same
        /// moment, as the mod framework writes one config file then.
        public Func<double>? JoinRunDelay { get; set; }

        /// Supplies the local player's held actions as a bit mask.
        public Func<ulong> ReadLocalInput { get; set; } = RealInput.Read;

        public int DamageTakenFromHost { get; private set; }
        public int HitsSent { get; private set; }
        public int AnimsSent { get; private set; }
        public int AnimsPlayed { get; private set; }
        public int Joins { get; private set; }
        public int HeroPackets { get; private set; }
        public int MobPackets { get; private set; }
        public double LastMobError { get; private set; }

        public NetSession(CoopSession coop, ITransport transport, NetRole role, ILogger log, int hostSeed)
        {
            this.coop = coop;
            this.transport = transport;
            this.log = log;
            Role = role;
            if (role == NetRole.Host)
            {
                Seed = hostSeed;
            }

            // The daily challenge is the same for everyone that day, so its seed stays.
            Hook_User.newGame += (orig, self, seed, data, a, b, mode) =>
                orig(self, Role == NetRole.Host && mode is LaunchMode.NewScoringGame ? seed : NextRunSeed() ?? seed, data, a, b, mode);
            // A run played together is nobody's own score. Only the end screen is told so:
            // asked at the start, the game would hold the run up with a warning about Steam.
            dc.ui.Hook_EndScoreMode.saveScore += (orig, self) =>
            {
                savingScore = true;
                orig(self);
                savingScore = false;
            };
            dc.tool.Hook__ServerApi.canSaveScore += orig => !(savingScore && PlayedTogether) && orig();
            Hook_Mob.tryToPreventDeath += OnMobTryToPreventDeath;
            Hook_Mob.onDie += OnMobDie;
            coop.RemoteHit = OnRemoteHit;
            // More players hit harder, and mob damage stays: each hero has the life of one.
            Hook_Mob.initLife += OnMobInitLife;
            Progress = new SessionProgress(log, () => peers.Keys);
            Look = new PartnerLook(log);
            Items = new SharedItems(log, SendForLevel);
            Life = new TeamLife(coop, log, SendForLevel, () => ReadLocalInput(), () => Slot);
            Gathering = new ExitGathering(coop, () => Role, log, SendForLevel, () => !PeerLeft, Life);
            Hook_Main.launchGame += OnLaunch;
        }

        void OnMobInitLife(Hook_Mob.orig_initLife orig, Mob self, double life, int? extra)
        {
            if (peers.Count == 0)
            {
                orig(self, life, extra);
                return;
            }
            var factor = TeamFactor(self);
            orig(self, life * factor, extra);
            mobScales[self] = factor;
        }

        /// Mobs made for fewer players get stronger when another one turns up, and
        /// weaker again when one is gone for good.
        void ScaleMobs()
        {
            foreach (var mob in Registry?.Mobs ?? [])
            {
                if (mob.destroyed || mob.life <= 0)
                {
                    continue;
                }
                var applied = mobScales.GetValueOrDefault(mob, 1);
                var wanted = TeamFactor(mob);
                if (wanted == applied)
                {
                    continue;
                }
                var factor = wanted / applied;
                mob.maxLife = (int)System.Math.Round(mob.maxLife * factor);
                mob.life = System.Math.Max(1, (int)System.Math.Round(mob.life * factor));
                mobScales[mob] = wanted;
            }
        }

        double TeamFactor(Mob mob) => 1 + (LifeScale(mob) - 1) * peers.Count;

        static ulong OwnId => Steamworks.SteamAPI.IsSteamRunning() ? Steamworks.SteamUser.GetSteamID().m_SteamID : 0;

        /// Each run needs its own levels. The host picks them; the others take what they were told.
        int? NextRunSeed() => Role == NetRole.Host ? Seed + runsStarted++ : Seed;

        /// The host links to everyone; the others only to the host.
        bool Direct(Peer peer) => Role == NetRole.Host || peer.Slot == SteamTransport.HostLink;

        bool Matches(Peer peer) => Registry != null && peer.Arrival is { } arrival && arrival.Number == levelNumber && arrival.Room == room
            && peer.Signatures.TryGetValue((levelNumber, room), out var signature) && signature == Registry.Signature;

        Peer PeerFor(int slot)
        {
            if (!peers.TryGetValue(slot, out var peer))
            {
                peer = new Peer(slot);
                peers[slot] = peer;
            }
            return peer;
        }

        /// Puts this session on a new link: a room was opened or joined.
        public void Attach(ITransport link, NetRole role)
        {
            transport.Dispose();
            transport = link;
            Role = role;
            if (role == NetRole.Client)
            {
                Seed = null;
            }
            ForgetLink();
        }

        /// Back to being alone, with the others informed.
        public void Leave()
        {
            transport.SendReliable(Packet.Build(PacketType.Bye, _ => { }));
            GoOnAlone();
            Detach();
        }

        /// The host's decision; the room's owner sends this player away.
        public void Kick(ulong player)
        {
            foreach (var peer in peers.Values.Where(p => p.SteamId == player && Direct(p)).ToList())
            {
                transport.Send(peer.Slot, Packet.Build(PacketType.Kicked, _ => { }), true);
                dropped.Add(peer.Slot);
                DropPeer(peer.Slot);
            }
        }

        /// The room says the host is gone before the link gives up on it.
        public void HostLeft()
        {
            if (Role == NetRole.Client)
            {
                log.Information("RINGO the host left the room");
                GoOnAlone();
            }
        }

        void Detach()
        {
            retiring?.Dispose();
            retiring = transport;
            retiringFor = 0;
            transport = new IdleTransport();
            ForgetLink();
        }

        void UpdateRetiring(double dt)
        {
            if (retiring == null)
            {
                return;
            }
            retiring.Update(dt);
            while (retiring.TryReceive(out _, out _))
            {
            }
            retiringFor += dt;
            if (retiringFor >= RetireSeconds)
            {
                retiring.Dispose();
                retiring = null;
            }
        }

        void ForgetLink()
        {
            everLinked = false;
            PeerLeft = false;
            peers.Clear();
            remembered.Clear();
            dropped.Clear();
            ownSlot = 0;
            seedTold = null;
            runTold = null;
            modeTold = null;
            hostInRun = false;
            Progress.NewLink();
            Look.NewLink();
        }

        void OnLaunch(Hook_Main.orig_launchGame orig, Main self, LaunchMode mode, bool? a, double? b)
        {
            if (HostLeadsStart && !launching && transport.Connected)
            {
                if (Role == NetRole.Client)
                {
                    log.Information("RINGO start ignored: the host starts the run");
                    Refused?.Invoke(Phrase.OnlyHostStarts);
                    return;
                }
                if (!ReadyToStart)
                {
                    log.Information("RINGO start ignored: still exchanging progress");
                    Refused?.Invoke(Phrase.StillSyncing);
                    return;
                }
            }
            if (Role == NetRole.Host)
            {
                hostMode = RunMode.From(mode);
                log.Information("RINGO starting a {k} run", hostMode.Kind);
            }
            orig(self, mode, a, b);
        }

        /// The host tells where things stand, and again whenever that changes.
        void SayHello()
        {
            if (Role != NetRole.Host || !transport.Connected)
            {
                return;
            }
            var game = Game.Class.ME;
            var inRun = game?.data != null;
            var seed = inRun ? game!.data.gameSeed : Seed! + runsStarted;
            if (seed != seedTold || inRun != runTold || hostMode != modeTold)
            {
                seedTold = seed;
                runTold = inRun;
                modeTold = hostMode;
                transport.SendReliable(Packet.Build(PacketType.Hello, w =>
                {
                    w.Write(seed!.Value);
                    w.Write(inRun);
                    hostMode.Write(w);
                }));
            }
        }

        void OnHello(BinaryReader reader)
        {
            Seed = reader.ReadInt32();
            hostInRun = reader.ReadBoolean();
            hostMode = RunMode.Read(reader);
            log.Information("RINGO host seed {s}, in a run={r}, {k}", Seed, hostInRun, hostMode.Kind);
            if (hostInRun && Game.Class.ME == null)
            {
                Progress.JoiningRun = true;
            }
        }

        /// Whatever the host started, the others join; the same goes for coming back after a crash.
        void JoinRun(double dt)
        {
            if (!HostLeadsStart || Role != NetRole.Client || !hostInRun || !Progress.Ready || Seed == startedSeed)
            {
                joinWait = 0;
                return;
            }
            var atTitle = Main.Class.ME?.curProcess is TitleScreen;
            // The host can also start a new run from inside one, through the Boss Rush door for one.
            var otherRun = Game.Class.ME?.data is { } data && data.gameSeed != Seed && !Gathering.InTransit;
            if (atTitle || Life.Wiped || otherRun)
            {
                joinWait += dt;
                if (joinWait < (JoinRunDelay?.Invoke() ?? 0))
                {
                    return;
                }
                log.Information("RINGO joining the host's {k} run, seed {s}", hostMode.Kind, Seed);
                startedSeed = Seed;
                launching = true;
                Main.Class.ME!.launchGame(hostMode.ToLaunch(), null, null);
                launching = false;
            }
        }

        void TrackLinks(double dt)
        {
            var links = transport.Links;
            dropped.RemoveWhere(link => !links.Contains(link));
            foreach (var link in links.Where(l => !dropped.Contains(l)).ToList())
            {
                var peer = PeerFor(link);
                peer.DownSeconds = 0;
                if (!peer.Up)
                {
                    peer.Up = true;
                    OnLinkUp(peer);
                }
            }
            foreach (var peer in peers.Values.Where(p => p.Up && !links.Contains(p.Slot)).ToList())
            {
                peer.Up = false;
                OnLinkLost(peer);
            }
            foreach (var peer in peers.Values.Where(p => Direct(p) && !p.Up).ToList())
            {
                peer.DownSeconds += dt;
                if (peer.DownSeconds < GiveUpSeconds)
                {
                    continue;
                }
                if (Role == NetRole.Client)
                {
                    log.Information("RINGO the host is gone, going on alone");
                    GoOnAlone();
                    return;
                }
                log.Information("RINGO player {p} is gone", peer.Slot);
                DropPeer(peer.Slot);
            }
        }

        void OnLinkLost(Peer peer)
        {
            log.Information("RINGO the link to player {p} is down", peer.Slot);
            // Mobs must not waste their attention on a hero nobody controls right now.
            // The host's link carries everyone else's heroes too.
            foreach (var remote in coop.Remotes.Values.Where(r => Role == NetRole.Client || r.Slot == peer.Slot))
            {
                remote.set_targetable(false);
            }
            if (Role == NetRole.Client)
            {
                Gathering.Reset();
            }
            else
            {
                Gathering.ForgetPeer(peer.Slot);
            }
            Notice.Say(Phrase.LinkLost);
        }

        /// What travelled while the link was down is lost, so each side says again
        /// where it stands. Hero and mob states are sent all the time anyway.
        void OnLinkUp(Peer peer)
        {
            var cameBack = peer.EverUp;
            log.Information("RINGO the link to player {p} is up after {s:0.0}s, came back={b}", peer.Slot, peer.DownSeconds, cameBack);
            peer.EverUp = true;
            everLinked = true;
            PeerLeft = false;
            seedTold = null;
            runTold = null;
            signatureSent = false;
            peer.Signatures.Remove((levelNumber, room));
            Progress.NewLink(Role == NetRole.Host ? peer.Slot : null);
            Look.NewLink();
            foreach (var remote in coop.Remotes.Values.Where(r => Role == NetRole.Client || r.Slot == peer.Slot))
            {
                remote.set_targetable(!remote.Downed && !remote.Away);
            }
            if (Role == NetRole.Client)
            {
                var fresh = Game.Class.ME == null;
                transport.SendReliable(Packet.Build(PacketType.Join, w =>
                {
                    w.Write(fresh);
                    w.Write(OwnId);
                }));
            }
            Notice.Say(cameBack ? Phrase.LinkBack : Phrase.PartnerHere);
        }

        /// A player who joins hears who they are and what the others said so far. One
        /// who comes back gets what they carried when they entered their last level.
        void OnJoin(int slot, BinaryReader reader)
        {
            var fresh = reader.ReadBoolean();
            var id = reader.ReadUInt64();
            var peer = PeerFor(slot);
            peer.SteamId = id;
            Joins++;
            log.Information("RINGO player {p} joined, fresh={f}", slot, fresh);
            // Someone back under another number leaves the old one behind.
            foreach (var old in peers.Values.Where(p => p.Slot != slot && p.SteamId == id && id != 0 && !p.Up).ToList())
            {
                DropPeer(old.Slot);
            }
            // Someone quick to restart replaces the old link before it has timed out,
            // so the link may never have looked down. They still need to hear everything.
            seedTold = null;
            runTold = null;
            signatureSent = false;
            peer.Signatures.Remove((levelNumber, room));
            Progress.NewLink(slot);
            Look.NewLink();
            transport.Send(slot, Packet.Build(PacketType.Welcome, w => w.Write((byte)slot)), true);
            foreach (var ((from, _), packet) in remembered.Where(r => r.Key.Slot != slot).ToList())
            {
                transport.Send(slot, RelayPacket(from, packet), true);
            }
            if (fresh && savedLoadouts.TryGetValue(id, out var loadout))
            {
                transport.Send(slot, Packet.Build(PacketType.LoadoutBack, w => w.Write(loadout)), true);
            }
        }

        /// A player is gone: their hero goes, and the others hear of it.
        void DropPeer(int slot)
        {
            if (!peers.Remove(slot))
            {
                return;
            }
            log.Information("RINGO player {p} left the session", slot);
            coop.Remove(slot);
            Life.ForgetPeer(slot);
            Gathering.ForgetPeer(slot);
            Progress.ForgetPeer(slot);
            Look.ForgetPeer(slot);
            foreach (var key in remembered.Keys.Where(k => k.Slot == slot).ToList())
            {
                remembered.Remove(key);
            }
            if (Role == NetRole.Host)
            {
                transport.SendReliable(Packet.Build(PacketType.PeerGone, w => w.Write((byte)slot)));
            }
            if (peers.Count == 0)
            {
                GoOnAlone();
                return;
            }
            ScaleMobs();
            Notice.Say(Phrase.PartnerLeft);
        }

        /// The one who stays goes on alone. A player who joined has only puppets, so it
        /// takes the mobs over, and the room with them: the others may come back to it.
        void GoOnAlone()
        {
            PeerLeft = true;
            peers.Clear();
            remembered.Clear();
            ScaleMobs();
            foreach (var slot in coop.Remotes.Keys.ToList())
            {
                coop.Remove(slot);
            }
            coop.Forget();
            Life.Reset();
            Gathering.Reset();
            if (Role == NetRole.Client)
            {
                Role = NetRole.Host;
                // The seed it heard was this run's, and the next run needs another one.
                runsStarted = 1;
                foreach (var mob in Registry?.Mobs ?? [])
                {
                    mob.unlockAi();
                }
                // Its link only led to the host that is gone.
                Detach();
                PeerLeft = true;
            }
            Notice.Say(Phrase.PartnerLeft);
        }

        static double LifeScale(Mob mob) =>
            mob is dc.en.mob.Boss ? BossLifeScale : mob.elite ? EliteLifeScale : MobLifeScale;

        /// Everything about a level carries its number: an arriving player must not
        /// apply what is left of the previous level, or of one it has not reached.
        void SendForLevel(PacketType type, Action<BinaryWriter> body) =>
            transport.SendReliable(LevelPacket(type, body));

        byte[] LevelPacket(PacketType type, Action<BinaryWriter> body) => Packet.Build(type, w =>
        {
            w.Write(levelNumber);
            w.Write(room);
            body(w);
        });

        static byte[] RelayPacket(int slot, byte[] packet) => Packet.Build(PacketType.Relay, w =>
        {
            w.Write((byte)slot);
            w.Write(packet);
        });

        public void Update(double dt)
        {
            UpdateRetiring(dt);
            transport.Update(dt);
            TrackLinks(dt);
            SayHello();
            if (transport.Connected)
            {
                Progress.Send(transport.SendReliable);
                Look.Send(transport.SendReliable);
            }
            // In this order: what piled up during a load must meet the new level number.
            TrackLevel();
            Receive();
            AnnounceReady();
            OfferContents();
            JoinRun(dt);
            FollowPeer();
            // A run's heroes end with its game, and the next run has a game of its own.
            var game = Game.Class.ME;
            if (coop.Remotes.Count > 0 && !ReferenceEquals(game, runGame))
            {
                log.Information("RINGO the run is over");
                coop.Forget();
                Life.Reset();
                Gathering.Reset();
            }
            if (game?.hero == null)
            {
                return;
            }
            UpdateRemotes(game);
            ScaleMobs();
            TellWaiting();
            if (!coop.Remotes.Values.Any(r => !r.Away))
            {
                return;
            }
            RestoreLoadout();
            foreach (var remote in coop.Remotes.Values)
            {
                Look.Update(remote);
            }
            Life.Update(dt);
            Watch(game);
            Gathering.Update(dt);
            Items.Update();
            heroTimer += dt;
            if (heroTimer >= HeroSendSeconds)
            {
                heroTimer = 0;
                SendHero();
            }
            if (Role == NetRole.Host)
            {
                mobTimer += dt;
                if (mobTimer >= MobSendSeconds)
                {
                    mobTimer = 0;
                    SendMobs();
                }
            }
        }

        /// A player's hero appears once they are in the same level as this one, and is
        /// away while they are somewhere else.
        void UpdateRemotes(Game game)
        {
            if (transport.Connected && CoopSession.CanSpawn(game) && !game.hasCinematic())
            {
                foreach (var peer in peers.Values.Where(p => !coop.Remotes.ContainsKey(p.Slot) && Matches(p)).ToList())
                {
                    coop.Spawn(game, peer.Slot);
                    runGame = game;
                }
            }
            foreach (var remote in coop.Remotes.Values)
            {
                remote.SetAway(!(peers.TryGetValue(remote.Slot, out var peer) && Matches(peer)));
            }
        }

        /// A save continued by the host has a level whose mobs are partly gone, which
        /// no other game can rebuild. The players meet in the next one instead.
        void TellWaiting()
        {
            if (!saidWaiting && Role == NetRole.Client && peers.TryGetValue(SteamTransport.HostLink, out var host)
                && host.Arrival is { } arrival && arrival.Number == levelNumber && arrival.Room == room
                && host.Signatures.ContainsKey((levelNumber, room)) && Registry != null && !Matches(host))
            {
                saidWaiting = true;
                Notice.Say(Phrase.MeetInNextLevel);
            }
        }

        void RestoreLoadout()
        {
            if (loadoutToRestore is { } bytes)
            {
                loadoutToRestore = null;
                using var reader = new BinaryReader(new MemoryStream(bytes));
                Loadout.Apply(reader, Game.Class.ME.hero);
                log.Information("RINGO this player's gear is back");
            }
        }

        void TrackLevel()
        {
            var level = Game.Class.ME?.curLevel;
            if (level == null)
            {
                return;
            }
            if (ReferenceEquals(level, trackedLevel))
            {
                // A new level comes with rooms of its own, so the ones known are this level's.
                if (ReferenceEquals(Game.Class.ME, trackedGame))
                {
                    RememberRooms(Game.Class.ME, level);
                }
                return;
            }
            var game = Game.Class.ME;
            if (ReferenceEquals(game, trackedGame) && IsSideRoomMove(game, level))
            {
                EnterRoom(game, level);
                return;
            }
            if (!ReferenceEquals(game, trackedGame))
            {
                // Someone joining heard the host's way before their first level.
                if (trackedGame != null)
                {
                    foreach (var peer in peers.Values)
                    {
                        peer.Levels.Clear();
                    }
                }
                trackedGame = game;
                runLevels.Clear();
                routeFound = false;
                // A continued save starts deep in a run. Its levels so far are the way there.
                foreach (var id in WaySoFar(game))
                {
                    runLevels[++levelNumber] = id;
                }
            }
            trackedLevel = level;
            levelNumber++;
            room = 0;
            roomsMain = level;
            roomsSeen.Clear();
            roomRegistries.Clear();
            RememberRooms(game, level);
            runLevels[levelNumber] = CurrentLevelId(game);
            foreach (var peer in peers.Values)
            {
                peer.ContentsSent = false;
            }
            foreach (var mob in mobScales.Keys.Where(m => m.destroyed).ToList())
            {
                mobScales.Remove(mob);
            }
            signatureSent = false;
            itemsTracked = false;
            saidWaiting = false;
            dead.Clear();
            lastAnims.Clear();
            Gathering.Reset();
            Items.Clear();
            Life.OnNewLevel();
            Registry = new MobRegistry(level);
            roomRegistries[level] = Registry;
            log.Information("RINGO level {l} registered {n} mobs signature {s:x}", levelNumber, Registry.Mobs.Count, Registry.Signature);
            LockPuppets();
        }

        void LockPuppets()
        {
            if (Role == NetRole.Client)
            {
                foreach (var mob in Registry!.Mobs)
                {
                    mob.lockAiS(PuppetAiLockSeconds);
                }
            }
        }

        /// The game builds a level's side rooms along with it and switches between them.
        void RememberRooms(Game game, Level level)
        {
            roomsSeen.Add(level);
            var rooms = game.subLevels;
            for (var i = 0; rooms != null && i < rooms.length; i++)
            {
                if (rooms.getDyn(i) is Level other)
                {
                    roomsSeen.Add(other);
                }
            }
        }

        bool IsSideRoomMove(Game game, Level level) => trackedLevel != null && roomsSeen.Contains(level);

        /// Into a side room or back out of it: the same level number, the room's own
        /// mobs, and nothing that only a new level brings.
        void EnterRoom(Game game, Level level)
        {
            trackedLevel = level;
            room = ReferenceEquals(level, roomsMain) ? (byte)0 : (byte)(RoomIndex(game, level) + 1);
            foreach (var peer in peers.Values)
            {
                peer.ContentsSent = false;
            }
            signatureSent = false;
            itemsTracked = false;
            saidWaiting = false;
            lastAnims.Clear();
            Gathering.Reset();
            Items.Clear();
            if (roomRegistries.TryGetValue(level, out var registry))
            {
                Registry = registry;
            }
            else
            {
                Registry = new MobRegistry(level);
                roomRegistries[level] = Registry;
                LockPuppets();
            }
            log.Information("RINGO level {l} room {r} {id}: {n} mobs signature {s:x}",
                levelNumber, room, CurrentLevelId(game), Registry.Mobs.Count, Registry.Signature);
        }

        static int RoomIndex(Game game, Level level)
        {
            var rooms = game.subLevels;
            for (var i = 0; rooms != null && i < rooms.length; i++)
            {
                if (ReferenceEquals(rooms.getDyn(i), level))
                {
                    return i;
                }
            }
            return byte.MaxValue - 1;
        }

        /// The levels a run went through before this one, as its save keeps them for the world map.
        static List<string> WaySoFar(Game game)
        {
            var way = new List<string>();
            var times = game.data?.gameTimePerLevel;
            for (var i = 0; times != null && i < times.length; i++)
            {
                if (((dynamic)times.getDyn(i)).id?.ToString() is string id)
                {
                    way.Add(id);
                }
            }
            if (way.Count > 0 && way[^1] == CurrentLevelId(game))
            {
                way.RemoveAt(way.Count - 1);
            }
            return way;
        }

        /// Only once the hero is in the level, so the other games wait for a player who is really there.
        void AnnounceReady()
        {
            var game = Game.Class.ME;
            if (signatureSent || Registry == null || !CoopSession.CanSpawn(game) || game!.hasCinematic())
            {
                return;
            }
            signatureSent = true;
            if (!itemsTracked)
            {
                itemsTracked = true;
                Items.TrackLevel(game.curLevel);
            }
            SendForLevel(PacketType.LevelReady, w =>
            {
                w.Write(Registry.Signature);
                w.Write(CurrentLevelId(game));
                w.Write(runLevels.Count);
                foreach (var (number, id) in runLevels)
                {
                    w.Write(number);
                    w.Write(id);
                }
            });
            // Everyone keeps everyone's gear: whoever stays may end up hosting the one who comes back.
            if (loadoutToRestore == null)
            {
                SaveLoadoutNow();
            }
        }

        public void SaveLoadoutNow() => SendForLevel(PacketType.LoadoutSaved, w =>
        {
            w.Write(OwnId);
            Loadout.Write(w, Game.Class.ME.hero);
        });

        /// The same level that does not match: the host tells what is in its own, and
        /// the other player may find it holds just the same and more.
        void OfferContents()
        {
            if (Role != NetRole.Host || Registry == null || Game.Class.ME is not { } game)
            {
                return;
            }
            foreach (var peer in peers.Values)
            {
                if (peer.ContentsSent || peer.Arrival is not { } arrival || arrival.Number != levelNumber || arrival.Room != room
                    || arrival.Id != CurrentLevelId(game) || !peer.Signatures.ContainsKey((levelNumber, room)) || Matches(peer))
                {
                    continue;
                }
                peer.ContentsSent = true;
                var keys = Registry.Keys;
                log.Information("RINGO player {p}'s level differs, sending its {n} contents", peer.Slot, keys.Count);
                transport.Send(peer.Slot, LevelPacket(PacketType.LevelContents, w =>
                {
                    w.Write(keys.Count);
                    foreach (var key in keys)
                    {
                        w.Write(key);
                    }
                }), true);
            }
        }

        void OnContents(BinaryReader reader)
        {
            var keys = new List<string>();
            for (var left = reader.ReadInt32(); left > 0; left--)
            {
                keys.Add(reader.ReadString());
            }
            if (Role != NetRole.Client || Registry == null || LevelsMatch || Game.Class.ME?.curLevel is not { } level)
            {
                return;
            }
            if (!Registry.TrimTo(keys))
            {
                log.Information("RINGO the host's level is not part of this one");
                return;
            }
            // What lay around is tracked again, so what was just taken out does not count as taken.
            Items.Clear();
            Registry = new MobRegistry(level, skipGone: true);
            roomRegistries[level] = Registry;
            LockPuppets();
            itemsTracked = false;
            signatureSent = false;
            saidWaiting = false;
            log.Information("RINGO level trimmed to the host's: {n} mobs, signature {s:x}, match={m}",
                Registry.Mobs.Count, Registry.Signature, LevelsMatch);
        }

        /// A player who is down watches someone else fight on.
        void Watch(Game game)
        {
            if (game.curLevel?.viewport is not { } viewport)
            {
                return;
            }
            var standing = coop.Remotes.Values.FirstOrDefault(r => !r.Downed && !r.Away && r._level != null);
            var watchOther = Life.Local != DownState.Standing && standing != null;
            Entity target = watchOther ? standing! : game.hero;
            var tracked = viewport.tracked;
            // A cinematic points the camera somewhere else, and keeps it.
            if (!ReferenceEquals(tracked, target) && (ReferenceEquals(tracked, game.hero) || tracked is RemoteHero))
            {
                viewport.track(target, null);
            }
        }

        static string CurrentLevelId(Game game) => game.curLevel.map.infos.id.ToString();

        /// What happened in this level before a player got here, or while they were away.
        void ReplayLevel()
        {
            if (Role == NetRole.Host && Registry != null)
            {
                // The list itself knows best: mobs can also end without ever calling onDie.
                var gone = Registry.Mobs.Where(m => m.destroyed || m.life <= 0).ToList();
                log.Information("RINGO telling the others about {n} dead mobs", gone.Count);
                foreach (var mob in gone)
                {
                    Registry.TryGetIndex(mob, out var index);
                    SendForLevel(PacketType.MobDied, w =>
                    {
                        w.Write(index);
                        w.Write(true);
                    });
                }
            }
            Items.Replay();
        }

        /// Doors gather everyone, but a level also ends in falls and hidden passages
        /// that one player takes alone. The others follow whoever is furthest ahead.
        /// Someone joining a run does the same from its first level, however far along
        /// the host is.
        void FollowPeer()
        {
            var game = Game.Class.ME;
            if (peers.Count == 0 || LinkDown || !transport.Connected || Gathering.InTransit || Life.Wiped
                || !CoopSession.CanSpawn(game) || game!.hasCinematic())
            {
                return;
            }
            // A player still in another run than the host's is on its way to the host's run instead.
            if (Role == NetRole.Client && game.data?.gameSeed != Seed)
            {
                return;
            }
            FindRoute(game);
            Peer? leader = null;
            foreach (var peer in peers.Values)
            {
                if (peer.Arrival is { } a && (sbyte)(a.Number - levelNumber) > 0
                    && (leader == null || (sbyte)(a.Number - leader.Arrival!.Value.Number) > 0))
                {
                    leader = peer;
                }
            }
            var ahead = leader != null;
            // Side rooms are entered together through their doors. A player in one
            // comes back out of it; nobody follows them in.
            if (!ahead && Role == NetRole.Client && peers.TryGetValue(SteamTransport.HostLink, out var host)
                && host.Arrival is { } h && h.Number == levelNumber && h.Room == 0 && room == 0 && h.Id != CurrentLevelId(game))
            {
                leader = host;
            }
            if (leader?.Arrival is not { } arrival)
            {
                return;
            }
            // A level's content depends on the way there, so the way is walked level by level.
            var known = leader.Levels.Keys.Where(n => (sbyte)(n - levelNumber) > 0).OrderBy(n => (sbyte)(n - levelNumber)).ToList();
            byte next;
            string id;
            if (ahead && known.Count > 0)
            {
                next = known[0];
                id = leader.Levels[next];
                log.Information("RINGO following player {p}'s way, {id} of {last}", leader.Slot, id, arrival.Id);
            }
            else if (arrival.Room == 0)
            {
                id = arrival.Id;
                next = arrival.Number;
                log.Information("RINGO following player {p} to {id}", leader.Slot, id);
            }
            else
            {
                return;
            }
            Gathering.InTransit = true;
            levelNumber = (byte)(next - 1);
            LevelTransition.Class.@goto.Invoke(id.AsHaxeString());
        }

        /// Someone joining counted their own levels from one, wherever the host's count
        /// stands. Finding their level on the host's way tells which number it has there.
        void FindRoute(Game game)
        {
            if (routeFound || Role != NetRole.Client || !peers.TryGetValue(SteamTransport.HostLink, out var host) || host.Levels.Count == 0)
            {
                return;
            }
            routeFound = true;
            var here = CurrentLevelId(game);
            foreach (var (number, id) in host.Levels.OrderBy(l => l.Key))
            {
                if (id == here)
                {
                    levelNumber = number;
                    return;
                }
            }
        }

        void SendHero()
        {
            var hero = Game.Class.ME.hero;
            var state = new HeroStateData
            {
                // Interacting is the owner's business: doors go through the gathering,
                // and a replay must not open anything in another player's world.
                Held = ReadLocalInput() & ~(1UL << HeroAction.Interact),
                Down = Life.Local,
                Cx = hero.cx,
                Cy = hero.cy,
                Dir = hero.dir,
                Life = hero.life,
                Xr = (float)hero.xr,
                Yr = (float)hero.yr,
                Dx = (float)hero.dx,
                Dy = (float)hero.dy,
            };
            transport.SendUnreliable(LevelPacket(PacketType.HeroState, state.Write));
        }

        void SendMobs()
        {
            // A level holds well over a hundred mobs, and most are nowhere near anyone.
            var heroes = coop.Remotes.Values.Where(r => !r.Away).Cast<Hero>().Append(Game.Class.ME.hero).ToList();
            var alive = Registry!.Mobs.Where(m => !m.destroyed && m.life > 0 && heroes.Any(h => Near(m, h))).ToList();
            var packet = LevelPacket(PacketType.MobStates, w =>
            {
                w.Write((ushort)alive.Count);
                foreach (var mob in alive)
                {
                    Registry.TryGetIndex(mob, out var index);
                    SendAnimIfChanged(mob, index);
                    new MobStateData
                    {
                        Index = index,
                        Cx = mob.cx,
                        Cy = mob.cy,
                        Dir = mob.dir,
                        Life = mob.life,
                        Xr = (float)mob.xr,
                        Yr = (float)mob.yr,
                        Dx = (float)mob.dx,
                        Dy = (float)mob.dy,
                    }.Write(w);
                }
            });
            transport.SendUnreliable(packet);
        }

        static bool Near(Mob mob, Hero hero) =>
            System.Math.Abs(mob.cx - hero.cx) <= MobSyncRangeCells && System.Math.Abs(mob.cy - hero.cy) <= MobSyncRangeCells;

        /// A puppet has no AI to pick its animations, and without them a player
        /// could not see an attack coming. Only mobs in play are worth the traffic.
        void SendAnimIfChanged(Mob mob, ushort index)
        {
            if (mob.isOutOfGame || mob.spr?.groupName?.ToString() is not { } group)
            {
                return;
            }
            if (!lastAnims.TryGetValue(index, out var last) || last != group)
            {
                lastAnims[index] = group;
                SendForLevel(PacketType.MobAnim, w =>
                {
                    w.Write(index);
                    w.Write(group);
                });
                AnimsSent++;
            }
        }

        void ApplyAnim(ushort index, string group)
        {
            if (Registry?.Get(index) is not { destroyed: false } mob || mob.spr is not { } sprite
                || sprite.groupName?.ToString() == group)
            {
                return;
            }
            sprite.get_anim().play(group.AsHaxeString(), null, null);
            AnimsPlayed++;
        }

        /// Handles what is about the session rather than a level. True when it was that.
        bool ReceiveOutsideLevels(int slot, PacketType type, BinaryReader reader)
        {
            var fromHost = Role == NetRole.Client && slot == SteamTransport.HostLink;
            switch (type)
            {
                case PacketType.Hello:
                    if (fromHost)
                    {
                        OnHello(reader);
                    }
                    return true;

                case PacketType.Progress:
                    Progress.OnPeerProgress(slot, reader);
                    return true;

                case PacketType.Join:
                    if (Role == NetRole.Host)
                    {
                        OnJoin(slot, reader);
                    }
                    return true;

                case PacketType.LoadoutBack:
                    loadoutToRestore = reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));
                    return true;

                case PacketType.Bye:
                    if (Role == NetRole.Host)
                    {
                        dropped.Add(slot);
                        DropPeer(slot);
                    }
                    else if (fromHost)
                    {
                        log.Information("RINGO the host left the room");
                        GoOnAlone();
                    }
                    return true;

                case PacketType.Look:
                    Look.OnLook(slot, reader);
                    return true;

                case PacketType.Kicked:
                    log.Information("RINGO the host removed this player from the room");
                    GoOnAlone();
                    KickedOut?.Invoke();
                    return true;

                case PacketType.Welcome:
                    ownSlot = reader.ReadByte();
                    log.Information("RINGO this is player {p}", ownSlot);
                    return true;

                case PacketType.PeerGone:
                    var gone = reader.ReadByte();
                    if (fromHost && gone != ownSlot)
                    {
                        DropPeer(gone);
                    }
                    return true;

                case PacketType.Relay:
                    return true;

                default:
                    return false;
            }
        }

        void Receive()
        {
            while (transport.TryReceive(out var packet, out var link))
            {
                if (packet.Length == 0 || (Role == NetRole.Host && dropped.Contains(link)))
                {
                    continue;
                }
                // The others' words reach a player who joined through the host, with their number.
                if (Role == NetRole.Client && (PacketType)packet[0] == PacketType.Relay)
                {
                    if (packet.Length > 2)
                    {
                        Handle(packet[1], packet[2..]);
                    }
                    continue;
                }
                if (Role == NetRole.Host)
                {
                    PassOn(link, packet);
                }
                Handle(Role == NetRole.Host ? link : SteamTransport.HostLink, packet);
            }
        }

        /// The host tells the others what one player said about themselves.
        void PassOn(int slot, byte[] packet)
        {
            var type = (PacketType)packet[0];
            if (!Relayed.Contains(type))
            {
                return;
            }
            if (Remembered.Contains(type))
            {
                remembered[(slot, type)] = packet;
            }
            transport.SendAll(RelayPacket(slot, packet), type != PacketType.HeroState, slot);
        }

        void Handle(int slot, byte[] packet)
        {
            using var reader = new BinaryReader(new MemoryStream(packet));
            var type = (PacketType)reader.ReadByte();
            if (ReceiveOutsideLevels(slot, type, reader))
            {
                return;
            }
            var number = reader.ReadByte();
            var peerRoom = reader.ReadByte();
            var peer = PeerFor(slot);
            var fromHost = Role == NetRole.Client && slot == SteamTransport.HostLink;
            // Another game may announce a level this one has not reached yet.
            if (type == PacketType.LevelReady)
            {
                peer.Signatures[(number, peerRoom)] = reader.ReadUInt64();
                peer.Arrival = (number, peerRoom, reader.ReadString());
                for (var left = reader.ReadInt32(); left > 0; left--)
                {
                    peer.Levels[reader.ReadByte()] = reader.ReadString();
                }
                log.Information("RINGO player {p} level {l} room {r} {id} signature {s:x}, match={m}",
                    slot, number, peerRoom, peer.Arrival.Value.Id, peer.Signatures[(number, peerRoom)], Matches(peer));
                // Deaths name mobs by their place in the list, which must be the same list.
                if (number == levelNumber && peerRoom == room && Matches(peer))
                {
                    ReplayLevel();
                }
                return;
            }
            if (number != levelNumber || peerRoom != room)
            {
                return;
            }
            switch (type)
            {
                case PacketType.HeroState:
                    HeroPackets++;
                    var hero = HeroStateData.Read(reader);
                    coop.Remotes.GetValueOrDefault(slot)?.ApplyState(hero);
                    Life.OnPeerState(slot, hero.Down);
                    break;

                case PacketType.MobStates when fromHost && LevelsMatch:
                    ApplyMobs(reader);
                    break;

                case PacketType.MobDied when fromHost && LevelsMatch:
                    ApplyMobDeath(reader.ReadUInt16(), reader.ReadBoolean());
                    break;

                case PacketType.MobAnim when fromHost && LevelsMatch:
                    ApplyAnim(reader.ReadUInt16(), reader.ReadString());
                    break;

                case PacketType.HitHero when fromHost:
                    ApplyHit(reader.ReadInt32());
                    break;

                case PacketType.ExitWait:
                    Gathering.OnPeerDoor(slot, reader);
                    break;

                case PacketType.ExitGo when fromHost:
                    Gathering.OnProposal(reader);
                    break;

                case PacketType.ExitAccept when Role == NetRole.Host:
                    Gathering.OnAccepted();
                    break;

                case PacketType.Rescue:
                    Life.OnRescued(reader.ReadByte());
                    break;

                case PacketType.TeamWipe:
                    Life.OnWipe();
                    break;

                case PacketType.ItemTaken:
                    Items.OnTaken(reader);
                    break;

                case PacketType.ItemPlaced:
                    Items.OnPlaced(reader);
                    break;

                case PacketType.LoadoutSaved:
                    var owner = reader.ReadUInt64();
                    savedLoadouts[owner] = reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));
                    break;

                case PacketType.LevelContents when fromHost:
                    OnContents(reader);
                    break;
            }
        }

        void ApplyMobs(BinaryReader reader)
        {
            if (Registry == null || Role != NetRole.Client)
            {
                return;
            }
            MobPackets++;
            var count = reader.ReadUInt16();
            var error = 0.0;
            for (var i = 0; i < count; i++)
            {
                var state = MobStateData.Read(reader);
                if (Registry.Get(state.Index) is not { destroyed: false } mob)
                {
                    continue;
                }
                error += System.Math.Abs(mob.cx + mob.xr - state.Cx - state.Xr);
                mob.cx = state.Cx;
                mob.cy = state.Cy;
                mob.xr = state.Xr;
                mob.yr = state.Yr;
                mob.dx = state.Dx;
                mob.dy = state.Dy;
                mob.dir = state.Dir;
                mob.life = state.Life;
            }
            LastMobError = count > 0 ? error / count : 0;
        }

        /// A replayed death happened long ago, so that mob just is not there any more.
        void ApplyMobDeath(ushort index, bool replayed)
        {
            if (Registry?.Get(index) is { destroyed: false } mob)
            {
                if (replayed)
                {
                    mob.destroy();
                    return;
                }
                allowDeath = true;
                mob.life = 0;
                mob.kill();
                allowDeath = false;
                log.Information("RINGO mob {i} died by host order", index);
            }
        }

        void ApplyHit(int damage)
        {
            DamageTakenFromHost += damage;
            Life.TakeHit(Game.Class.ME.hero, damage);
        }

        bool OnMobTryToPreventDeath(Hook_Mob.orig_tryToPreventDeath orig, Mob self, dc.tool.atk.AttackData a, double previous)
        {
            if (Role == NetRole.Client && !allowDeath)
            {
                self.life = System.Math.Max(1, (int)previous);
                return true;
            }
            return orig(self, a, previous);
        }

        void OnMobDie(Hook_Mob.orig_onDie orig, Mob self)
        {
            orig(self);
            if (Role == NetRole.Host && Registry != null && Registry.TryGetIndex(self, out var index) && dead.TryAdd(self, true))
            {
                SendForLevel(PacketType.MobDied, w =>
                {
                    w.Write(index);
                    w.Write(false);
                });
            }
        }

        /// The host saw a mob hit another player's hero here; that player owns its life.
        void OnRemoteHit(int slot, int damage)
        {
            if (Role == NetRole.Host)
            {
                HitsSent++;
                transport.Send(slot, LevelPacket(PacketType.HitHero, w => w.Write(damage)), true);
            }
        }
    }
}
