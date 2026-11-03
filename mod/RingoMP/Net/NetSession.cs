using dc;
using dc.cine;
using dc.en;
using dc.pr;
using ModCore.Utilities;
using Serilog;

namespace RingoMP.Net
{
    public enum NetRole { Host, Client }

    /// Keeps two games in step. The host owns mobs: where they are, their life and
    /// their death. Each player owns their own hero. A client's mobs are puppets,
    /// and its hits on them only show; the real ones come from its RemoteHero
    /// replaying the same inputs on the host.
    /// A session lives as long as the game does. Alone it just watches, so a friend
    /// can join at any moment: same seed, same levels, straight to where the host is.
    public sealed class NetSession
    {
        const double HeroSendSeconds = 1.0 / 30;
        const double MobSendSeconds = 1.0 / 15;
        const double PuppetAiLockSeconds = 100000;
        const double MobLifeScale = 1.5;
        const double EliteLifeScale = 1.7;
        const double BossLifeScale = 1.8;
        const int MobSyncRangeCells = 60;

        readonly CoopSession coop;
        readonly ILogger log;
        readonly Dictionary<Mob, bool> dead = new(ReferenceEqualityComparer.Instance);
        readonly Dictionary<ushort, string> lastAnims = [];
        readonly Dictionary<(byte Number, byte Room), ulong> peerSignatures = [];
        readonly Dictionary<byte, string> runLevels = [];
        readonly Dictionary<byte, string> peerLevels = [];
        readonly HashSet<Mob> scaledMobs = new(ReferenceEqualityComparer.Instance);
        ITransport transport;
        Game? trackedGame;
        bool routeFound;
        double heroTimer;
        double mobTimer;
        (byte Number, byte Room, string Id)? peerArrival;
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
        bool linkUp;
        bool everLinked;
        bool launching;
        bool hostInRun;
        bool saidWaiting;
        bool contentsSent;
        int? seedTold;
        bool? runTold;
        int? startedSeed;
        ulong peerId;
        byte[]? savedLoadout;
        ulong savedLoadoutOwner;
        byte[]? loadoutToRestore;
        double downSeconds;

        public NetRole Role { get; private set; }

        /// How long a lost partner has to come back before they count as gone.
        public double GiveUpSeconds { get; set; } = 60;

        /// The link went down and may yet come back.
        public bool LinkDown => everLinked && !linkUp && !PeerLeft;

        /// The partner is gone for good and this player goes on alone.
        public bool PeerLeft { get; private set; }

        /// Players start runs from the title screen rather than a test: whatever the
        /// host starts, the client joins, and the client cannot start one by itself.
        public bool HostLeadsStart { get; init; }

        /// Says why an attempt to start was refused.
        public Action<Phrase>? Refused { get; set; }

        /// This player was kicked out of the room.
        public Action? KickedOut { get; set; }

        public ITransport Transport => transport;
        public int? Seed { get; private set; }
        public MobRegistry? Registry { get; private set; }
        public bool LevelsMatch => Registry != null && peerArrival is { } arrival && arrival.Number == levelNumber && arrival.Room == room
            && peerSignatures.TryGetValue((levelNumber, room), out var peer) && peer == Registry.Signature;

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

        /// A run can start: the seed and both players' progress are known.
        public bool ReadyToStart => Seed != null && Progress.Ready;

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

            Hook_User.newGame += (orig, self, seed, data, a, b, mode) => orig(self, NextRunSeed() ?? seed, data, a, b, mode);
            Hook_Mob.tryToPreventDeath += OnMobTryToPreventDeath;
            Hook_Mob.onDie += OnMobDie;
            coop.RemoteHit = OnRemoteHit;
            // Two players hit twice as hard, and mob damage stays: two heroes have the life of one each.
            Hook_Mob.initLife += OnMobInitLife;
            Progress = new SessionProgress(log);
            Look = new PartnerLook(log);
            Items = new SharedItems(log, SendForLevel);
            Life = new TeamLife(coop, log, SendForLevel, () => ReadLocalInput());
            Gathering = new ExitGathering(coop, () => Role, log, SendForLevel, () => !PeerLeft, Life);
            Hook_Main.launchGame += OnLaunch;
        }

        void OnMobInitLife(Hook_Mob.orig_initLife orig, Mob self, double life, int? extra)
        {
            if (!transport.Connected)
            {
                orig(self, life, extra);
                return;
            }
            orig(self, life * LifeScale(self), extra);
            scaledMobs.Add(self);
        }

        /// Mobs made for one player get stronger when a second one turns up, and
        /// weaker again when that player is gone for good.
        void ScaleMobs(bool together)
        {
            foreach (var mob in Registry?.Mobs ?? [])
            {
                if (mob.destroyed || mob.life <= 0 || scaledMobs.Contains(mob) == together)
                {
                    continue;
                }
                var factor = together ? LifeScale(mob) : 1 / LifeScale(mob);
                mob.maxLife = (int)System.Math.Round(mob.maxLife * factor);
                mob.life = System.Math.Max(1, (int)System.Math.Round(mob.life * factor));
                if (together)
                {
                    scaledMobs.Add(mob);
                }
                else
                {
                    scaledMobs.Remove(mob);
                }
            }
        }

        static ulong OwnId => Steamworks.SteamAPI.IsSteamRunning() ? Steamworks.SteamUser.GetSteamID().m_SteamID : 0;

        /// Each run needs its own levels. The host picks them; a client takes what it was told.
        int? NextRunSeed() => Role == NetRole.Host ? Seed + runsStarted++ : Seed;

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

        /// Back to being alone, with the partner informed.
        public void Leave()
        {
            transport.SendReliable(Packet.Build(PacketType.Bye, _ => { }));
            GoOnAlone();
            Detach();
        }

        /// The host's decision; the room's owner sends this player away.
        public void Kick()
        {
            transport.SendReliable(Packet.Build(PacketType.Kicked, _ => { }));
            GoOnAlone();
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
            transport.Dispose();
            transport = new IdleTransport();
            ForgetLink();
        }

        void ForgetLink()
        {
            linkUp = false;
            everLinked = false;
            downSeconds = 0;
            PeerLeft = false;
            peerArrival = null;
            peerSignatures.Clear();
            peerLevels.Clear();
            seedTold = null;
            runTold = null;
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
            if (seed != seedTold || inRun != runTold)
            {
                seedTold = seed;
                runTold = inRun;
                transport.SendReliable(Packet.Build(PacketType.Hello, w =>
                {
                    w.Write(seed!.Value);
                    w.Write(inRun);
                }));
            }
        }

        void OnHello(BinaryReader reader)
        {
            Seed = reader.ReadInt32();
            hostInRun = reader.ReadBoolean();
            log.Information("RINGO host seed {s}, in a run={r}", Seed, hostInRun);
            if (hostInRun && Game.Class.ME == null)
            {
                Progress.JoiningRun = true;
            }
        }

        /// Whatever the host started, the client joins; the same goes for coming back after a crash.
        void JoinRun()
        {
            if (!HostLeadsStart || Role != NetRole.Client || !hostInRun || !Progress.Ready || Seed == startedSeed)
            {
                return;
            }
            var atTitle = Main.Class.ME?.curProcess is TitleScreen;
            if (atTitle || Life.Wiped)
            {
                log.Information("RINGO joining the host's run, seed {s}", Seed);
                startedSeed = Seed;
                launching = true;
                Main.Class.ME!.launchGame(new LaunchMode.NewGame(false, false), null, null);
                launching = false;
            }
        }

        void TrackLink(double dt)
        {
            if (transport.Connected)
            {
                if (!linkUp)
                {
                    linkUp = true;
                    OnLinkUp();
                }
                downSeconds = 0;
                return;
            }
            if (linkUp)
            {
                linkUp = false;
                OnLinkLost();
            }
            if (LinkDown)
            {
                downSeconds += dt;
                if (downSeconds >= GiveUpSeconds)
                {
                    log.Information("RINGO the partner is gone, going on alone");
                    GoOnAlone();
                }
            }
        }

        void OnLinkLost()
        {
            log.Information("RINGO the link to the partner is down");
            Gathering.Reset();
            // Mobs must not waste their attention on a hero nobody controls right now.
            coop.Remote?.set_targetable(false);
            Notice.Say(Phrase.LinkLost);
        }

        /// What travelled while the link was down is lost, so each side says again
        /// where it stands. Hero and mob states are sent all the time anyway.
        void OnLinkUp()
        {
            var cameBack = everLinked;
            log.Information("RINGO the link is up after {s:0.0}s, came back={b}", downSeconds, cameBack);
            everLinked = true;
            PeerLeft = false;
            seedTold = null;
            runTold = null;
            signatureSent = false;
            peerSignatures.Remove((levelNumber, room));
            Progress.NewLink();
            Look.NewLink();
            if (coop.Remote is { } remote)
            {
                remote.set_targetable(!remote.Downed);
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

        /// A player who comes back gets what they carried when they entered their last level.
        void OnJoin(BinaryReader reader)
        {
            var fresh = reader.ReadBoolean();
            peerId = reader.ReadUInt64();
            Joins++;
            log.Information("RINGO a player joined, fresh={f}", fresh);
            // Someone quick to restart replaces the old link before it has timed out,
            // so the link may never have looked down. They still need to hear everything.
            seedTold = null;
            runTold = null;
            signatureSent = false;
            peerSignatures.Remove((levelNumber, room));
            Progress.NewLink();
            Look.NewLink();
            if (fresh && savedLoadout != null && savedLoadoutOwner == peerId)
            {
                var loadout = savedLoadout;
                transport.SendReliable(Packet.Build(PacketType.LoadoutBack, w => w.Write(loadout)));
            }
        }

        /// The one who stays goes on alone. A client has only puppets, so it takes the
        /// mobs over, and the room with them: the other player may come back to it.
        void GoOnAlone()
        {
            PeerLeft = true;
            peerArrival = null;
            ScaleMobs(false);
            coop.Remote?.destroy();
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

        public void Update(double dt)
        {
            transport.Update(dt);
            TrackLink(dt);
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
            JoinRun();
            FollowPeer();
            // A run's heroes end with its game, and the next run has a game of its own.
            var game = Game.Class.ME;
            if (coop.Remote != null && !ReferenceEquals(game, runGame))
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
            if (coop.Remote == null && transport.Connected && LevelsMatch && CoopSession.CanSpawn(game) && !game.hasCinematic())
            {
                coop.Spawn(game);
                runGame = game;
            }
            if (coop.Remote == null || !LevelsMatch)
            {
                TellWaiting();
                return;
            }
            RestoreLoadout();
            ScaleMobs(true);
            Look.Update(coop.Remote);
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

        /// A save continued by the host has a level whose mobs are partly gone, which
        /// no other game can rebuild. The players meet in the next one instead.
        void TellWaiting()
        {
            if (!saidWaiting && Role == NetRole.Client && peerArrival is { } arrival && arrival.Number == levelNumber && arrival.Room == room
                && peerSignatures.ContainsKey((levelNumber, room)) && Registry != null && !LevelsMatch)
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
                    peerLevels.Clear();
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
            contentsSent = false;
            scaledMobs.RemoveWhere(m => m.destroyed);
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
            contentsSent = false;
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

        /// Only once the hero is in the level, so the other game waits for a player who is really there.
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
            // Both keep the other's gear: whoever stays may end up hosting the one who comes back.
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
        /// the client may find it holds just the same and more.
        void OfferContents()
        {
            if (Role != NetRole.Host || contentsSent || Registry == null || Game.Class.ME is not { } game
                || peerArrival is not { } arrival || arrival.Number != levelNumber || arrival.Room != room || arrival.Id != CurrentLevelId(game)
                || !peerSignatures.ContainsKey((levelNumber, room)) || LevelsMatch)
            {
                return;
            }
            contentsSent = true;
            var keys = Registry.Keys;
            log.Information("RINGO the partner's level differs, sending its {n} contents", keys.Count);
            SendForLevel(PacketType.LevelContents, w =>
            {
                w.Write(keys.Count);
                foreach (var key in keys)
                {
                    w.Write(key);
                }
            });
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

        /// A player who is down watches their partner fight on.
        void Watch(Game game)
        {
            if (game.curLevel?.viewport is not { } viewport)
            {
                return;
            }
            var remote = coop.Remote;
            var watchPartner = Life.Local != DownState.Standing && remote is { Downed: false, _level: not null };
            Entity target = watchPartner ? remote! : game.hero;
            var tracked = viewport.tracked;
            // A cinematic points the camera somewhere else, and keeps it.
            if (!ReferenceEquals(tracked, target) && (ReferenceEquals(tracked, game.hero) || ReferenceEquals(tracked, remote)))
            {
                viewport.track(target, null);
            }
        }

        static string CurrentLevelId(Game game) => game.curLevel.map.infos.id.ToString();

        /// What happened in this level before the partner got here, or while they were away.
        void ReplayLevel()
        {
            if (Role == NetRole.Host && Registry != null)
            {
                // The list itself knows best: mobs can also end without ever calling onDie.
                var gone = Registry.Mobs.Where(m => m.destroyed || m.life <= 0).ToList();
                log.Information("RINGO telling the partner about {n} dead mobs", gone.Count);
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

        /// Doors gather both players, but a level also ends in falls and hidden
        /// passages that one player takes alone. The other one follows. Someone joining
        /// a run does the same from its first level, however far along the host is.
        void FollowPeer()
        {
            var game = Game.Class.ME;
            if (peerArrival is not { } arrival || LinkDown || !transport.Connected || Gathering.InTransit || Life.Wiped
                || !CoopSession.CanSpawn(game) || game!.hasCinematic())
            {
                return;
            }
            FindRoute(game);
            var ahead = (sbyte)(arrival.Number - levelNumber) > 0;
            // Side rooms are entered together through their doors. A partner in one
            // comes back out of it; nobody follows them in.
            var elsewhere = Role == NetRole.Client && arrival.Number == levelNumber && arrival.Room == 0 && room == 0
                && arrival.Id != CurrentLevelId(game);
            if (!ahead && !elsewhere)
            {
                return;
            }
            // A level's content depends on the way there, so the way is walked level by level.
            var known = peerLevels.Keys.Where(n => (sbyte)(n - levelNumber) > 0).OrderBy(n => (sbyte)(n - levelNumber)).ToList();
            byte next;
            string id;
            if (ahead && known.Count > 0)
            {
                next = known[0];
                id = peerLevels[next];
                log.Information("RINGO following the other player's way, {id} of {last}", id, arrival.Id);
            }
            else if (arrival.Room == 0)
            {
                id = arrival.Id;
                next = arrival.Number;
                log.Information("RINGO following the other player to {id}", id);
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
            if (routeFound || peerLevels.Count == 0)
            {
                return;
            }
            routeFound = true;
            var here = CurrentLevelId(game);
            foreach (var (number, id) in peerLevels.OrderBy(l => l.Key))
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
                // and a replay must not open anything in the other player's world.
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
            var main = Game.Class.ME.hero;
            var remote = coop.Remote!;
            var alive = Registry!.Mobs.Where(m => !m.destroyed && m.life > 0 && (Near(m, main) || Near(m, remote))).ToList();
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
        bool ReceiveOutsideLevels(PacketType type, BinaryReader reader)
        {
            switch (type)
            {
                case PacketType.Hello:
                    OnHello(reader);
                    return true;

                case PacketType.Progress:
                    Progress.OnPeerProgress(reader);
                    return true;

                case PacketType.Join:
                    OnJoin(reader);
                    return true;

                case PacketType.LoadoutBack:
                    loadoutToRestore = reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));
                    return true;

                case PacketType.Bye:
                    log.Information("RINGO the partner left the room");
                    GoOnAlone();
                    return true;

                case PacketType.Look:
                    Look.OnLook(reader);
                    return true;

                case PacketType.Kicked:
                    log.Information("RINGO the host removed this player from the room");
                    GoOnAlone();
                    KickedOut?.Invoke();
                    return true;

                default:
                    return false;
            }
        }

        void Receive()
        {
            while (transport.TryReceive(out var packet))
            {
                using var reader = new BinaryReader(new MemoryStream(packet));
                var type = (PacketType)reader.ReadByte();
                if (ReceiveOutsideLevels(type, reader))
                {
                    continue;
                }
                var number = reader.ReadByte();
                var peerRoom = reader.ReadByte();
                // The other game may announce a level this one has not reached yet.
                if (type == PacketType.LevelReady)
                {
                    peerSignatures[(number, peerRoom)] = reader.ReadUInt64();
                    peerArrival = (number, peerRoom, reader.ReadString());
                    for (var left = reader.ReadInt32(); left > 0; left--)
                    {
                        peerLevels[reader.ReadByte()] = reader.ReadString();
                    }
                    log.Information("RINGO peer level {l} room {r} {id} signature {s:x}, match={m}",
                        number, peerRoom, peerArrival.Value.Id, peerSignatures[(number, peerRoom)], LevelsMatch);
                    // Deaths name mobs by their place in the list, which must be the same list.
                    if (number == levelNumber && peerRoom == room && LevelsMatch)
                    {
                        ReplayLevel();
                    }
                    continue;
                }
                if (number != levelNumber || peerRoom != room)
                {
                    continue;
                }
                switch (type)
                {
                    case PacketType.HeroState:
                        HeroPackets++;
                        var hero = HeroStateData.Read(reader);
                        coop.Remote?.ApplyState(hero);
                        Life.OnPeerState(hero.Down);
                        break;

                    case PacketType.MobStates when LevelsMatch:
                        ApplyMobs(reader);
                        break;

                    case PacketType.MobDied when LevelsMatch:
                        ApplyMobDeath(reader.ReadUInt16(), reader.ReadBoolean());
                        break;

                    case PacketType.MobAnim when LevelsMatch:
                        ApplyAnim(reader.ReadUInt16(), reader.ReadString());
                        break;

                    case PacketType.HitHero:
                        ApplyHit(reader.ReadInt32());
                        break;

                    case PacketType.ExitWait:
                        Gathering.OnPeerDoor(reader);
                        break;

                    case PacketType.ExitGo:
                        Gathering.OnProposal(reader);
                        break;

                    case PacketType.ExitAccept:
                        Gathering.OnAccepted();
                        break;

                    case PacketType.Rescue:
                        Life.OnRescued();
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
                        savedLoadoutOwner = reader.ReadUInt64();
                        savedLoadout = reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));
                        break;

                    case PacketType.LevelContents:
                        OnContents(reader);
                        break;
                }
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

        /// The host saw a mob hit the client's hero here; the client owns that hero's life.
        void OnRemoteHit(int damage)
        {
            if (Role == NetRole.Host)
            {
                HitsSent++;
                SendForLevel(PacketType.HitHero, w => w.Write(damage));
            }
        }
    }
}
