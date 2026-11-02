using dc;
using dc.en;
using dc.pr;
using dc.tool.atk;
using Serilog;

namespace RingoMP.Net
{
    public enum DownState : byte
    {
        Standing,
        Rescuable,
        WaitingForNextLevel,
    }

    /// A player out of life goes down instead of dying. The partner can stand them
    /// up for a while; after that they are back in the next level. Only a team with
    /// nobody left standing dies, and then both players die together.
    public sealed class TeamLife
    {
        const double RescueWindowSeconds = 30;
        const double RescueHoldSeconds = 1.5;
        const int RescueRangeCells = 3;
        const double StandUpLifeRatio = 0.5;
        const double LockSeconds = 100000;
        const float DownedAlpha = 0.5f;
        const float StandingAlpha = 1;
        const int DownedLife = 1;

        readonly CoopSession coop;
        readonly ILogger log;
        readonly Action<PacketType, Action<BinaryWriter>> send;
        readonly Func<ulong> localInput;
        double downTimer;
        double holdTimer;
        bool wiped;

        public DownState Local { get; private set; }
        public DownState Peer { get; private set; }

        /// The run is lost, and the game is on its way to the death screen.
        public bool Wiped => wiped;

        public TeamLife(CoopSession coop, ILogger log, Action<PacketType, Action<BinaryWriter>> send, Func<ulong> localInput)
        {
            this.coop = coop;
            this.log = log;
            this.send = send;
            this.localInput = localInput;
            Hook_Hero.tryToPreventDeath += OnTryToPreventDeath;
            Hook_Hero.canBeHitBy += OnCanBeHitBy;
        }

        bool OnTryToPreventDeath(Hook_Hero.orig_tryToPreventDeath orig, Hero self, AttackData a, double previous)
        {
            var saved = orig(self, a, previous);
            // Items that cheat death have had their say, and left the life above this line.
            if (self.life < 1 && coop.Remote != null && !wiped)
            {
                GoDown();
                return true;
            }
            return saved;
        }

        bool OnCanBeHitBy(Hook_Hero.orig_canBeHitBy orig, Hero self, Entity by) =>
            (self is RemoteHero || Local == DownState.Standing) && orig(self, by);

        /// The host landed a hit that belongs to this player's hero.
        public void TakeHit(Hero hero, int damage)
        {
            if (Local != DownState.Standing)
            {
                return;
            }
            hero.life -= damage;
            if (hero.life < 1)
            {
                GoDown();
            }
            hero.updateLifeBar();
        }

        public void GoDown()
        {
            var hero = Game.Class.ME.hero;
            hero.life = DownedLife;
            if (Local != DownState.Standing)
            {
                return;
            }
            log.Information("RINGO this player went down");
            Local = DownState.Rescuable;
            downTimer = 0;
            hero.cancelVelocities();
            hero.lockControlsS(LockSeconds);
            hero.set_targetable(false);
            hero.spr.alpha = DownedAlpha;
            Notice.Say("你倒下了，等待队友救援", "You are down. Wait for your partner");
            if (Peer != DownState.Standing)
            {
                Wipe();
            }
        }

        void StandUp()
        {
            var hero = Game.Class.ME.hero;
            log.Information("RINGO this player stood up");
            Local = DownState.Standing;
            hero.life = System.Math.Max(DownedLife, (int)(hero.maxLife * StandUpLifeRatio));
            hero.unlockControls();
            hero.set_targetable(true);
            hero.spr.alpha = StandingAlpha;
            hero.updateLifeBar();
        }

        void Wipe()
        {
            if (wiped)
            {
                return;
            }
            wiped = true;
            log.Information("RINGO nobody left standing, the run ends");
            send(PacketType.TeamWipe, _ => { });
            Die();
        }

        void Die()
        {
            wiped = true;
            var hero = Game.Class.ME.hero;
            Local = DownState.Standing;
            hero.unlockControls();
            hero.set_targetable(true);
            hero.spr.alpha = StandingAlpha;
            hero.life = 0;
            hero.kill();
        }

        public void OnWipe() => Die();

        public void OnPeerState(DownState state)
        {
            if (state == Peer)
            {
                return;
            }
            var wasStanding = Peer == DownState.Standing;
            Peer = state;
            if (state == DownState.Standing)
            {
                coop.Remote?.Revive(coop.Remote.maxLife);
            }
            else if (wasStanding)
            {
                coop.Remote?.GoDown();
                Notice.Say("队友倒下了，靠近并按住交互键救援", "Your partner is down. Hold interact next to them");
                if (Local != DownState.Standing)
                {
                    Wipe();
                }
            }
        }

        public void OnRescued()
        {
            if (Local == DownState.Rescuable)
            {
                StandUp();
                Notice.Say("队友把你救起来了", "Your partner got you up");
            }
        }

        /// A new level brings a player who was not rescued back.
        public void OnNewLevel()
        {
            holdTimer = 0;
            if (Local != DownState.Standing)
            {
                StandUp();
            }
        }

        public void Update(double dt)
        {
            if (Local == DownState.Rescuable)
            {
                downTimer += dt;
                if (downTimer >= RescueWindowSeconds)
                {
                    Local = DownState.WaitingForNextLevel;
                    Notice.Say("救援时间已过，下一关复活", "Too late for rescue. You return in the next level");
                }
            }
            UpdateRescue(dt);
        }

        void UpdateRescue(double dt)
        {
            var hero = Game.Class.ME.hero;
            var remote = coop.Remote;
            var holding = (localInput() >> HeroAction.Interact & 1) != 0;
            var inRange = remote != null && System.Math.Abs(hero.cx - remote.cx) <= RescueRangeCells
                && System.Math.Abs(hero.cy - remote.cy) <= RescueRangeCells;
            if (Peer != DownState.Rescuable || Local != DownState.Standing || !holding || !inRange)
            {
                holdTimer = 0;
                return;
            }
            holdTimer += dt;
            if (holdTimer >= RescueHoldSeconds)
            {
                holdTimer = 0;
                log.Information("RINGO rescued the partner");
                send(PacketType.Rescue, _ => { });
            }
        }

        public void Reset()
        {
            Local = DownState.Standing;
            Peer = DownState.Standing;
            wiped = false;
            holdTimer = 0;
        }
    }
}
