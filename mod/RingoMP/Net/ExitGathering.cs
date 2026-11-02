using dc.en;
using dc.en.inter;
using dc.pr;
using Serilog;

namespace RingoMP.Net
{
    /// Nobody leaves a level alone. Activating a door marks the player as waiting
    /// there; pressing again or walking away takes it back, and the choice stays
    /// open until both players wait at the same door. The host then proposes and
    /// the client answers, so a client that just cancelled is never pulled along.
    public sealed class ExitGathering
    {
        const int LeaveRangeCells = 3;
        const double ResendSeconds = 1;

        readonly CoopSession coop;
        readonly Func<NetRole> role;
        readonly ILogger log;
        readonly Action<PacketType, Action<BinaryWriter>> send;
        readonly Func<bool> connected;
        readonly TeamLife life;
        Exit? localExit;
        bool localFlag;
        (int X, int Y)? peerDoor;
        bool proposed;
        bool leaving;
        double resendTimer;

        public bool LocalWaiting => localExit != null;
        public bool PeerWaiting => peerDoor != null;

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
            Hook_Exit.onActivate += OnActivate;
        }

        void OnActivate(Hook_Exit.orig_onActivate orig, Exit self, Hero by, bool flag)
        {
            if (by is RemoteHero)
            {
                return;
            }
            if (leaving || self.locked || coop.Remote == null || !connected())
            {
                orig(self, by, flag);
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
                Notice.Say("等待队友进门，再按一次取消", "Waiting for your partner. Press again to cancel");
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
            Notice.Say("已取消进门", "Cancelled");
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

        Exit? FindExit((int X, int Y) door)
        {
            var entities = Game.Class.ME.curLevel.entities;
            for (var i = 0; i < entities.length; i++)
            {
                if (entities.getDyn(i) is Exit exit && exit.cx == door.X && exit.cy == door.Y)
                {
                    return exit;
                }
            }
            return null;
        }

        public void Update(double dt)
        {
            // A player who is down cannot walk, so the partner's door is theirs too.
            var down = life.Local != DownState.Standing;
            if (down && localExit == null && peerDoor is { } door)
            {
                localExit = FindExit(door);
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
            var partnerReady = LocalDoor == peerDoor || life.Peer != DownState.Standing;
            if (role() == NetRole.Host && !proposed && localExit != null && partnerReady)
            {
                proposed = true;
                send(PacketType.ExitGo, w =>
                {
                    w.Write(localExit.cx);
                    w.Write(localExit.cy);
                });
            }
        }

        public void OnPeerDoor(BinaryReader reader)
        {
            var waiting = reader.ReadBoolean();
            var door = (reader.ReadInt32(), reader.ReadInt32());
            var next = waiting ? door : ((int, int)?)null;
            if (next == peerDoor)
            {
                return;
            }
            peerDoor = next;
            if (!waiting)
            {
                proposed = false;
                return;
            }
            PeerWaits++;
            if (localExit == null || LocalDoor == next)
            {
                Notice.Say("队友已在门口等待", "Your partner is at the door");
            }
            else
            {
                Notice.Say("队友在另一扇门等待", "Your partner is at another door");
            }
        }

        /// A proposal for a door the client no longer waits at is ignored: its
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

        public void OnAccepted() => Leave();

        void Leave()
        {
            var exit = localExit!;
            log.Information("RINGO leaving through the door at {x},{y} to {d}", exit.cx, exit.cy, exit.destLevel?.ToString());
            leaving = true;
            exit.onActivate(Game.Class.ME.hero, localFlag);
            leaving = false;
            Reset();
            InTransit = true;
        }

        public void Reset()
        {
            localExit = null;
            peerDoor = null;
            proposed = false;
            InTransit = false;
        }
    }
}
