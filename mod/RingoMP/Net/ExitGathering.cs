using dc;
using dc.en;
using dc.en.inter;
using dc.en.inter.zdoor;
using dc.pr;
using Serilog;

namespace RingoMP.Net
{
    /// Nobody leaves a level alone. Activating a door marks the player as waiting
    /// there; pressing again or walking away takes it back, and the choice stays
    /// open until every player waits at the same door. The host then proposes and
    /// the others answer, so a player who just cancelled is never pulled along.
    /// Doors are the exits to the next level and the ways into a level's side rooms
    /// and back: room doors, portals and entrance teleporters.
    public sealed class ExitGathering
    {
        const int LeaveRangeCells = 3;
        const double ResendSeconds = 1;
        // A door can still refuse once both are there, a locked room for one.
        const double RefusedSeconds = 5;

        readonly CoopSession coop;
        readonly Func<NetRole> role;
        readonly ILogger log;
        readonly Action<PacketType, Action<BinaryWriter>> send;
        readonly Func<bool> connected;
        readonly TeamLife life;
        Entity? localExit;
        bool localFlag;
        readonly Dictionary<int, (int X, int Y)> peerDoors = [];
        bool proposed;
        bool leaving;
        double resendTimer;
        double transitSeconds;

        public bool LocalWaiting => localExit != null;
        /// Someone else waits at a door.
        public bool PeerWaiting => peerDoors.Count > 0;

        /// Through a door or on the way after the other player, until the next level is here.
        public bool InTransit { get; set; }

        /// How many times the other player started waiting, for tests to follow along.
        public int PeerWaits { get; private set; }

        (int X, int Y)? LocalDoor => localExit == null ? null : (localExit.cx, localExit.cy);

        public ExitGathering(CoopSession coop, Func<NetRole> role, ILogger log, Action<PacketType, Action<BinaryWriter>> send, Func<bool> connected, TeamLife life)
        {
            this.life = life;
            this.coop = coop;
            this.role = role;
            this.log = log;
            this.send = send;
            this.connected = connected;
            Hook_Exit.onActivate += (orig, self, by, flag) => OnActivate(self, by, flag, self.locked, () => orig(self, by, flag));
            Hook_ZDoor.onActivate += (orig, self, by, flag) => OnActivate(self, by, flag, false, () => orig(self, by, flag));
            Hook_Portal.onActivate += (orig, self, by, flag) => OnActivate(self, by, flag, false, () => orig(self, by, flag));
            Hook_EntranceTeleporter.onActivate += (orig, self, by, flag) => OnActivate(self, by, flag, false, () => orig(self, by, flag));
            // These two room doors do their own activating, without the one they are made from.
            Hook_MonsterDoor.onActivate += (orig, self, by, flag) => OnActivate(self, by, flag, false, () => orig(self, by, flag));
            Hook_CliffEnigmaZDoor.onActivate += (orig, self, by, flag) => OnActivate(self, by, flag, false, () => orig(self, by, flag));
        }

        static bool IsDoor(object? entity) => entity is Exit or ZDoor or Portal or EntranceTeleporter;

        static void Activate(Entity door, Hero hero, bool flag)
        {
            switch (door)
            {
                case Exit exit:
                    exit.onActivate(hero, flag);
                    break;
                case MonsterDoor monsterDoor:
                    monsterDoor.onActivate(hero, flag);
                    break;
                case CliffEnigmaZDoor cliffDoor:
                    cliffDoor.onActivate(hero, flag);
                    break;
                case ChallengeZDoor challengeDoor:
                    challengeDoor.onActivate(hero, flag);
                    break;
                case TumulusAntichamberZDoor tumulusDoor:
                    tumulusDoor.onActivate(hero, flag);
                    break;
                case ZDoor zDoor:
                    zDoor.onActivate(hero, flag);
                    break;
                case Portal portal:
                    portal.onActivate(hero, flag);
                    break;
                case EntranceTeleporter teleporter:
                    teleporter.onActivate(hero, flag);
                    break;
            }
        }

        void OnActivate(Entity self, Hero by, bool flag, bool locked, Action orig)
        {
            if (by is RemoteHero)
            {
                return;
            }
            if (leaving || locked || coop.Remotes.Count == 0 || !connected())
            {
                orig();
                return;
            }
            if (ReferenceEquals(localExit, self))
            {
                Cancel();
            }
            else
            {
                localExit = self;
                localFlag = flag;
                SendDoor();
                Notice.Say(Phrase.DoorWaiting);
            }
        }

        void Cancel()
        {
            // The client may be on its way out already, so an open proposal binds the host.
            if (proposed)
            {
                return;
            }
            localExit = null;
            SendDoor();
            Notice.Say(Phrase.DoorCancelled);
        }

        void SendDoor()
        {
            var door = LocalDoor;
            send(PacketType.ExitWait, w =>
            {
                w.Write(door != null);
                w.Write(door?.X ?? 0);
                w.Write(door?.Y ?? 0);
            });
        }

        Entity? FindExit((int X, int Y) door)
        {
            var entities = Game.Class.ME.curLevel.entities;
            for (var i = 0; i < entities.length; i++)
            {
                if (entities.getDyn(i) is Entity exit && IsDoor(exit) && exit.cx == door.X && exit.cy == door.Y)
                {
                    return exit;
                }
            }
            return null;
        }

        public void Update(double dt)
        {
            if (InTransit)
            {
                transitSeconds += dt;
                if (transitSeconds >= RefusedSeconds && !Game.Class.ME.hasCinematic())
                {
                    log.Information("RINGO the door did not lead anywhere");
                    InTransit = false;
                }
            }
            // A player who is down cannot walk, so another player's door is theirs too.
            var down = life.Local != DownState.Standing;
            if (down && localExit == null && peerDoors.Count > 0)
            {
                localExit = FindExit(peerDoors.Values.First());
            }
            if (localExit != null)
            {
                var hero = Game.Class.ME.hero;
                if (!down && (System.Math.Abs(hero.cx - localExit.cx) > LeaveRangeCells || System.Math.Abs(hero.cy - localExit.cy) > LeaveRangeCells))
                {
                    Cancel();
                }
                // The other player may still be loading this level and have dropped the first one.
                resendTimer += dt;
                if (resendTimer >= ResendSeconds)
                {
                    resendTimer = 0;
                    SendDoor();
                }
            }
            var othersReady = coop.Remotes.Keys.All(slot =>
                (peerDoors.TryGetValue(slot, out var door) && LocalDoor == door) || life.PeerState(slot) != DownState.Standing);
            if (role() == NetRole.Host && !proposed && localExit != null && othersReady)
            {
                proposed = true;
                send(PacketType.ExitGo, w =>
                {
                    w.Write(localExit.cx);
                    w.Write(localExit.cy);
                });
            }
        }

        public void OnPeerDoor(int slot, BinaryReader reader)
        {
            var waiting = reader.ReadBoolean();
            var door = (reader.ReadInt32(), reader.ReadInt32());
            var next = waiting ? door : ((int, int)?)null;
            if (next == (peerDoors.TryGetValue(slot, out var known) ? known : ((int, int)?)null))
            {
                return;
            }
            if (!waiting)
            {
                peerDoors.Remove(slot);
                proposed = false;
                return;
            }
            peerDoors[slot] = door;
            PeerWaits++;
            if (localExit == null || LocalDoor == next)
            {
                Notice.Say(Phrase.DoorPartnerHere);
            }
            else
            {
                Notice.Say(Phrase.DoorPartnerElsewhere);
            }
        }

        /// A proposal for a door this player no longer waits at is ignored: its
        /// cancel is already on the way and clears the proposal on the host.
        public void OnProposal(BinaryReader reader)
        {
            var door = (reader.ReadInt32(), reader.ReadInt32());
            if (life.Local != DownState.Standing)
            {
                localExit = FindExit(door);
            }
            if (localExit != null && LocalDoor == door)
            {
                send(PacketType.ExitAccept, _ => { });
                Leave();
            }
        }

        /// The first answer is enough: the others heard the same proposal.
        public void OnAccepted()
        {
            if (localExit != null)
            {
                Leave();
            }
        }

        public void ForgetPeer(int slot) => peerDoors.Remove(slot);

        void Leave()
        {
            var exit = localExit!;
            log.Information("RINGO leaving through the {t} at {x},{y}{d}", exit.GetType().Name, exit.cx, exit.cy,
                exit is Exit { destLevel: { } dest } ? $" to {dest}" : "");
            leaving = true;
            Activate(exit, Game.Class.ME.hero, localFlag);
            leaving = false;
            Reset();
            InTransit = true;
            transitSeconds = 0;
        }

        public void Reset()
        {
            localExit = null;
            peerDoors.Clear();
            proposed = false;
            InTransit = false;
        }
    }
}
