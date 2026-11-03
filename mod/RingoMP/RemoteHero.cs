using dc;
using dc.en.hero;
using dc.pr;
using dc.tool.atk;
using HaxeProxy.Runtime;
using ModCore.Storage;
using ModCore.Utilities;
using RingoMP.Net;

namespace RingoMP
{
    /// Another player's hero, living in this machine's world.
    public class RemoteHero : Beheaded, IHxbitSerializable<object>
    {
        const int DownedLife = 1;
        const double SnapDriftCells = 3;
        const double AdjustDriftCells = 0.5;

        public VirtualController Input { get; }

        /// Which player this is: the host is 0, the others are numbered as they joined.
        public int Slot { get; init; }

        /// Set in a networked game; takes the damage this hero would have taken.
        public Action<int>? ReportHit { get; set; }

        public bool Downed { get; private set; }

        /// What the partner wears, where this game has it. The game draws a hero in
        /// what the local save says, so that is swapped while this hero is drawn.
        public (string? Skin, string? Head)? Outfit { get; set; }

        /// Its player is somewhere else, in a side room or still on the way here.
        public bool Away { get; private set; }

        public RemoteHero(Game game, VirtualController input) : base(game)
        {
            Input = input;
        }

        public int PreUpdates { get; private set; }

        public int FixedUpdates { get; private set; }

        /// Input advances with the hero's own reads, so a "just pressed" state
        /// lasts exactly one of them whatever the render rate is.
        public override void preUpdate()
        {
            PreUpdates++;
            Input.Update();
            Dressed(base.preUpdate);
        }

        public override void fixedUpdate()
        {
            FixedUpdates++;
            Dressed(base.fixedUpdate);
        }

        public override void postUpdate() => Dressed(base.postUpdate);

        public override void initGfx() => Dressed(base.initGfx);

        /// Builds this hero's graphics again in its outfit.
        public void Redress() => Dressed(() =>
        {
            disposeGfx();
            initGfx();
        });

        void Dressed(Action body)
        {
            var user = Game.Class.ME?.user;
            if (Outfit is not { } outfit || user == null)
            {
                body();
                return;
            }
            var (skin, head) = (user.heroSkin, user.heroHeadSkin);
            if (outfit.Skin != null)
            {
                user.heroSkin = outfit.Skin.AsHaxeString();
            }
            if (outfit.Head != null)
            {
                user.heroHeadSkin = outfit.Head.AsHaxeString();
            }
            try
            {
                body();
            }
            finally
            {
                user.heroSkin = skin;
                user.heroHeadSkin = head;
            }
        }

        /// Inputs keep this hero's attacks and effects alive between updates; the
        /// position it reports wins whenever the replay drifts away from it.
        public void ApplyState(HeroStateData state)
        {
            // Between levels this hero stands nowhere.
            if (_level == null)
            {
                return;
            }
            Input.HeldMask = state.Held;
            life = state.Life;
            dir = state.Dir;
            var drift = System.Math.Abs(cx + xr - state.Cx - state.Xr) + System.Math.Abs(cy + yr - state.Cy - state.Yr);
            if (drift > SnapDriftCells)
            {
                setPosCase(state.Cx, state.Cy, state.Xr, state.Yr);
            }
            else if (drift > AdjustDriftCells)
            {
                cx = state.Cx;
                cy = state.Cy;
                xr = state.Xr;
                yr = state.Yr;
            }
            dx = state.Dx;
            dy = state.Dy;
        }

        /// Life belongs to the player who owns this hero, so a hit taken here is
        /// reported and undone. The owner's next state brings the true value.
        public override void applyAttackResult(AttackData a)
        {
            var before = life;
            base.applyAttackResult(a);
            if (ReportHit != null)
            {
                if (life < before)
                {
                    ReportHit(before - life);
                }
                life = before;
            }
        }

        /// Belongs to the session, never to this player's save.
        public override bool shouldSave() => false;

        /// The game ignores this result and goes by life alone: below 1 it calls onDie.
        public override bool tryToPreventDeath(AttackData a, double prevLife)
        {
            if (ReportHit == null)
            {
                GoDown();
            }
            return true;
        }

        public override void kill()
        {
            if (ReportHit == null)
            {
                GoDown();
            }
        }

        /// Death of a remote hero must not reach the game over flow. Its owner decides it.
        public override void onDie()
        {
        }

        public override bool canBeHitBy(Entity by) => !Downed && !Away && base.canBeHitBy(by);

        /// A player who is not here leaves no hero standing around: it keeps its place,
        /// out of sight and out of the mobs' way, until they are back.
        public void SetAway(bool away)
        {
            if (away == Away)
            {
                return;
            }
            Away = away;
            Input.ReleaseAll();
            set_targetable(!away && !Downed);
            if (spr != null)
            {
                spr.visible = !away;
            }
        }

        public void GoDown()
        {
            Downed = true;
            life = DownedLife;
            Input.ReleaseAll();
            // Mobs keep their first target, so one that is down must drop out of their threat.
            set_targetable(false);
        }

        public void Revive(int newLife)
        {
            Downed = false;
            life = newLife;
            set_targetable(!Away);
        }

        object IHxbitSerializable<object>.GetData() => new();

        void IHxbitSerializable<object>.SetData(object data)
        {
        }
    }
}
