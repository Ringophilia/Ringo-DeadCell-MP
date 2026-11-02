using dc.cine;
using dc.en;
using dc.pr;
using ModCore.Utilities;
using Serilog;

namespace RingoMP.Dev
{
    /// Regression for the second hero: input, fight, going down, level changes.
    public sealed class M2Scenario(CoopSession session, ILogger log, string name) : ScenarioBase(session, log, name)
    {
        const double WalkSeconds = 1.5;
        const double JumpSeconds = 1;
        const double FightSeconds = 25;
        const double DeathSeconds = 12;
        const double ReviveCheckSeconds = 3;
        const double LevelLoadSeconds = 30;
        const double NewLevelSeconds = 4;
        const double ReportEvery = 2;
        const double AttackPulse = 0.3;
        const int MobApproachCells = 2;
        const int ReviveLife = 50;
        const string NextLevel = "PrisonCourtyard";
        const string SecondLevelId = "SewerShort";
        const int SecondLevel = 1;

        enum Step { Walk, Jump, Fight, Death, Revive, Travel, NewLevel }

        Step step;
        double elapsed;
        double nextReport;
        int travels;
        int startMainX, startRemoteX;
        double jumpStart, highest;
        int startLife, startMobLife;
        Mob? mob;
        Level? oldLevel;

        protected override void OnReady()
        {
            CoopSession.Give(Remote, "StartSword");
            Log.Information("RINGO remote items={r} main items={m} separate={sep}",
                Remote.inventory.items.length, CurrentGame.hero.inventory.items.length,
                !ReferenceEquals(Remote.inventory, CurrentGame.hero.inventory));
            startMainX = CurrentGame.hero.cx;
            startRemoteX = Remote.cx;
            Remote.Input.SetHeld(HeroAction.Right, true);
            Next(Step.Walk);
        }

        void Next(Step next)
        {
            step = next;
            elapsed = 0;
            nextReport = 0;
            Log.Information("RINGO --- {step}", next);
        }

        bool Due()
        {
            if (elapsed < nextReport)
            {
                return false;
            }
            nextReport = elapsed + ReportEvery;
            return true;
        }

        protected override void Play()
        {
            elapsed += FrameTime;
            switch (step)
            {
                case Step.Walk:
                    if (elapsed > WalkSeconds)
                    {
                        Remote.Input.ReleaseAll();
                        Log.Information("RINGO walk remote moved {r} cells, main moved {m} cells",
                            Remote.cx - startRemoteX, CurrentGame.hero.cx - startMainX);
                        jumpStart = Remote.cy + Remote.yr;
                        highest = jumpStart;
                        Remote.Input.SetHeld(HeroAction.Jump, true);
                        Next(Step.Jump);
                    }
                    break;

                case Step.Jump:
                    highest = System.Math.Min(highest, Remote.cy + Remote.yr);
                    if (elapsed > JumpSeconds)
                    {
                        Remote.Input.ReleaseAll();
                        Log.Information("RINGO jump rose {h:0.0} cells, ended {e:0.0} from {s:0.0}",
                            jumpStart - highest, Remote.cy + Remote.yr, jumpStart);
                        StartFight();
                    }
                    break;

                case Step.Fight:
                    Fight();
                    break;

                case Step.Death:
                    Death();
                    break;

                case Step.Revive:
                    if (elapsed > ReviveCheckSeconds)
                    {
                        Remote.Revive(ReviveLife);
                        Log.Information("RINGO revived downed={d} life={l} main life={m} cine={c}",
                            Remote.Downed, Remote.life, CurrentGame.hero.life, CurrentGame.curCine?.GetType().Name);
                        Travel(NextLevel);
                    }
                    break;

                case Step.Travel:
                    Travelling();
                    break;

                case Step.NewLevel:
                    NewLevel();
                    break;
            }
        }

        void Travel(string level)
        {
            oldLevel = CurrentGame.curLevel;
            LevelTransition.Class.@goto.Invoke(level.AsHaxeString());
            Next(Step.Travel);
        }

        void StartFight()
        {
            mob = NearestMob();
            if (mob == null)
            {
                Log.Warning("RINGO no mob in this level");
                Next(Step.Death);
                return;
            }
            Remote.setPosCase(mob.cx - MobApproachCells, mob.cy, null, null);
            startLife = Remote.life;
            startMobLife = mob.life;
            Log.Information("RINGO fighting {mob} life={life} at {x},{y}", mob.GetType().Name, mob.life, mob.cx, mob.cy);
            Next(Step.Fight);
        }

        Mob? NearestMob()
        {
            var entities = CurrentGame.curLevel.entities;
            Mob? best = null;
            var bestDistance = int.MaxValue;
            for (var i = 0; i < entities.length; i++)
            {
                if (entities.getDyn(i) is Mob m && m.life > 0 && !m.destroyed)
                {
                    var distance = System.Math.Abs(m.cx - Remote.cx) + System.Math.Abs(m.cy - Remote.cy);
                    if (distance < bestDistance)
                    {
                        best = m;
                        bestDistance = distance;
                    }
                }
            }
            return best;
        }

        void Fight()
        {
            var attacking = (int)(elapsed / AttackPulse) % 2 == 0;
            Remote.Input.SetHeld(HeroAction.Attack1, attacking);
            Remote.Input.SetHeld(HeroAction.Attack2, attacking);
            var dead = mob!.life <= 0 || mob.destroyed;
            var mobOnRight = mob.cx >= Remote.cx;
            Remote.Input.SetHeld(HeroAction.Right, mobOnRight);
            Remote.Input.SetHeld(HeroAction.Left, !mobOnRight);
            if (Due())
            {
                Log.Information("RINGO t={t:0} remote {x},{y} life={l} | mob {mx},{my} life={ml} aTarget={tgt} | main life={m}",
                    elapsed, Remote.cx, Remote.cy, Remote.life, mob.cx, mob.cy, mob.life,
                    mob.aTarget?.GetType().Name, CurrentGame.hero.life);
            }
            if (dead || Remote.Downed || elapsed > FightSeconds)
            {
                Remote.Input.ReleaseAll();
                Log.Information("RINGO fight over: mob {ms}->{me} remote {rs}->{re} downed={d}",
                    startMobLife, mob.life, startLife, Remote.life, Remote.Downed);
                Next(Step.Death);
            }
        }

        void Death()
        {
            if (Remote.Downed)
            {
                Log.Information("RINGO remote downed: destroyed={d} main life={m} game.hero is main={same} cine={c}",
                    Remote.destroyed, CurrentGame.hero.life, !ReferenceEquals(CurrentGame.hero, Remote), CurrentGame.curCine?.GetType().Name);
                Next(Step.Revive);
                return;
            }
            if (elapsed < 0.1)
            {
                Remote.life = 1;
            }
            if (elapsed > DeathSeconds)
            {
                Log.Information("RINGO no mob finished it, calling kill()");
                Remote.kill();
            }
        }

        void Travelling()
        {
            var game = CurrentGame;
            var arrived = game.curLevel != null && !ReferenceEquals(game.curLevel, oldLevel) && game.hero.awake && game.hero._level != null;
            if (arrived && Remote._level != null)
            {
                startRemoteX = Remote.cx;
                Remote.Input.SetHeld(HeroAction.Right, true);
                Next(Step.NewLevel);
            }
            else if (elapsed > LevelLoadSeconds)
            {
                Log.Warning("RINGO travel timed out");
                Finish();
            }
        }

        void NewLevel()
        {
            if (elapsed > NewLevelSeconds)
            {
                var game = CurrentGame;
                Remote.Input.ReleaseAll();
                Log.Information("RINGO new level {id}: remote in level={same} moved {m} cells life={l} items={i} destroyed={d}",
                    game.curLevel.map.id.ToString(), ReferenceEquals(Remote._level, game.curLevel),
                    Remote.cx - startRemoteX, Remote.life, Remote.inventory.items.length, Remote.destroyed);
                if (travels++ < SecondLevel)
                {
                    Travel(SecondLevelId);
                }
                else
                {
                    Finish();
                }
            }
        }
    }
}
