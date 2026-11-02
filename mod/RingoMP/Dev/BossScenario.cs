using dc.cine;
using dc.en;
using dc.en.mob;
using dc.pr;
using ModCore.Utilities;
using Serilog;

namespace RingoMP.Dev
{
    /// Takes both heroes to a boss level given by RINGO_BOSS_LEVEL, lets the remote
    /// hero fight the boss with the main hero far from it, then kills the boss.
    /// With RINGO_BOSS_SUBJECT=main the main hero stands at the boss instead, as a
    /// control: a boss that leaves it alone too has nothing to do with the remote hero.
    /// With RINGO_BOSS_SUBJECT=walk both heroes walk in from the entrance, for the
    /// bosses whose entrance cinematic waits for that.
    public sealed class BossScenario(CoopSession session, ILogger log, string name) : ScenarioBase(session, log, name)
    {
        const double LevelLoadSeconds = 40;
        const double ArenaGraceSeconds = 20;
        const double ArenaLimitSeconds = 30;
        const double FightSeconds = 24;
        const double FightCinematicSeconds = 10;
        const double FinishingSeconds = 12;
        const double AfterDeathSeconds = 10;
        const double ReportEvery = 3;
        const double AttackPulse = 0.3;
        const int MainDistanceCells = 16;
        const int RemoteDistanceCells = 3;
        const int FinishingLife = 1;
        const double RefillBelowRatio = 0.3;
        const int ControlLife = 100_000_000;
        const double ControlRepositionSeconds = 3;
        const double WalkInSeconds = 60;
        const double JumpPulse = 0.5;

        enum Step { Travel, Arena, Fight, Finishing, AfterDeath }

        readonly string level = Environment.GetEnvironmentVariable("RINGO_BOSS_LEVEL")!;
        readonly bool control = Environment.GetEnvironmentVariable("RINGO_BOSS_SUBJECT") == "main";
        readonly bool walkIn = Environment.GetEnvironmentVariable("RINGO_BOSS_SUBJECT") == "walk";
        readonly HashSet<string> bossAnims = [];
        VirtualController? mainInput;

        Step step;
        double elapsed;
        double nextReport;
        Level? oldLevel;
        Boss? boss;
        int hitsOnRemote;
        int damageOnRemote;
        int downs;
        int lastRemoteLife;
        bool cinematicClosed;
        double nextReposition;

        protected override void OnReady()
        {
            if (walkIn)
            {
                mainInput = new VirtualController();
                CurrentGame.hero.controller = mainInput.Access;
            }
            else
            {
                // Leaves one hero as the only target the boss can pick.
                (control ? Remote : CurrentGame.hero).set_targetable(false);
            }
            Hook_Hero.applyAttackResult += (orig, self, a) =>
            {
                Log.Information("RINGO boss hit {hero} source={s} dmg={d} t={t:0.0}",
                    self.GetType().Name, a.source?.GetType().Name, a.finalDmg, elapsed);
                orig(self, a);
            };
            CoopSession.Give(Remote, "StartSword");
            oldLevel = CurrentGame.curLevel;
            LevelTransition.Class.@goto.Invoke(level.AsHaxeString());
            Next(Step.Travel);
        }

        void Next(Step next)
        {
            step = next;
            elapsed = 0;
            nextReport = 0;
            Log.Information("RINGO --- {step}", next);
        }

        protected override void Play()
        {
            elapsed += FrameTime;
            mainInput?.Update();
            if (control || walkIn)
            {
                CurrentGame.hero.life = ControlLife;
            }
            else
            {
                KeepMainAlive();
            }
            switch (step)
            {
                case Step.Travel:
                    Travel();
                    break;

                case Step.Arena:
                    Arena();
                    break;

                case Step.Fight:
                    Fight();
                    break;

                case Step.Finishing:
                    Finishing();
                    break;

                case Step.AfterDeath:
                    if (elapsed > AfterDeathSeconds)
                    {
                        Log.Information("RINGO after boss death: main life={m} remote life={r} remote destroyed={d} cine={c}",
                            CurrentGame.hero.life, Remote.life, Remote.destroyed, CurrentGame.curCine?.GetType().Name);
                        Finish();
                    }
                    break;
            }
        }

        void Travel()
        {
            var game = CurrentGame;
            var arrived = game.curLevel != null && !ReferenceEquals(game.curLevel, oldLevel)
                && game.hero.awake && game.hero._level != null && Remote._level != null;
            if (arrived)
            {
                boss = FindBoss();
                Log.Information("RINGO arrived at {id} boss={b} cine={c}", level, boss?.GetType().Name, game.curCine?.GetType().Name);
                if (boss == null)
                {
                    Finish();
                    return;
                }
                if (!walkIn)
                {
                    game.hero.setPosCase(boss.cx - MainDistanceCells, boss.cy, null, null);
                }
                // A boss may wake on whoever it sees first, so the remote hero is there from the start.
                if (!control && !walkIn)
                {
                    Remote.setPosCase(boss.cx - MainDistanceCells, boss.cy, null, null);
                }
                Next(Step.Arena);
            }
            else if (elapsed > LevelLoadSeconds)
            {
                Log.Warning("RINGO travel timed out, cine={c}", game.curCine?.GetType().Name);
                Finish();
            }
        }

        static Boss? FindBoss()
        {
            var entities = CurrentGame.curLevel.entities;
            for (var i = 0; i < entities.length; i++)
            {
                if (entities.getDyn(i) is Boss b && !b.destroyed)
                {
                    return b;
                }
            }
            return null;
        }

        /// The entrance cinematic may take a while to start, so it gets time to run
        /// before the fight is started by hand.
        void Arena()
        {
            Remote.life = Remote.maxLife;
            var game = CurrentGame;
            if (walkIn)
            {
                HoldTowardBoss(mainInput!, game.hero);
                HoldTowardBoss(Remote.Input, Remote);
            }
            if (game.hasCinematic())
            {
                // These wait for the main hero to walk in, which a teleport never does.
                if (elapsed > (walkIn ? WalkInSeconds : ArenaLimitSeconds))
                {
                    Log.Warning("RINGO stuck in cinematic {c}, ready={r}, closing it", game.curCine?.GetType().Name, boss!.isReady());
                    game.curCine?.destroy();
                }
                return;
            }
            if (boss!.isReady())
            {
                Log.Information("RINGO boss ready by the game's own flow after {t:0}s", elapsed);
                StartFight();
            }
            else if (elapsed > (walkIn ? WalkInSeconds : ArenaGraceSeconds))
            {
                Log.Information("RINGO forcing boss ready");
                boss.setReady();
                StartFight();
            }
        }

        void HoldTowardBoss(VirtualController input, Hero hero)
        {
            var bossOnRight = boss!.cx >= hero.cx;
            input.SetHeld(HeroAction.Right, bossOnRight);
            input.SetHeld(HeroAction.Left, !bossOnRight);
            input.SetHeld(HeroAction.Jump, (int)(elapsed / JumpPulse) % 2 == 0);
        }

        void StartFight()
        {
            if (!control && !walkIn)
            {
                Remote.setPosCase(boss!.cx - RemoteDistanceCells, boss.cy, null, null);
            }
            lastRemoteLife = Remote.life;
            Next(Step.Fight);
        }

        void StayAtBoss()
        {
            if (elapsed >= nextReposition)
            {
                nextReposition = elapsed + ControlRepositionSeconds;
                CurrentGame.hero.setPosCase(boss!.cx - RemoteDistanceCells, boss.cy, null, null);
            }
        }

        void Press()
        {
            if (control)
            {
                StayAtBoss();
                return;
            }
            var attacking = (int)(elapsed / AttackPulse) % 2 == 0;
            Remote.Input.SetHeld(HeroAction.Attack1, attacking);
            Remote.Input.SetHeld(HeroAction.Attack2, attacking);
            var bossOnRight = boss!.cx >= Remote.cx;
            Remote.Input.SetHeld(HeroAction.Right, bossOnRight);
            Remote.Input.SetHeld(HeroAction.Left, !bossOnRight);
        }

        void TrackRemote()
        {
            if (Remote.life < lastRemoteLife)
            {
                hitsOnRemote++;
                damageOnRemote += lastRemoteLife - Remote.life;
            }
            // A boss hit takes a starting hero straight down, and a downed hero cannot be hit.
            if (Remote.Downed)
            {
                downs++;
                Remote.Revive(Remote.maxLife);
            }
            else if (Remote.life < Remote.maxLife * RefillBelowRatio)
            {
                Remote.life = Remote.maxLife;
            }
            lastRemoteLife = Remote.life;
        }

        /// Some entrance cinematics only start once the boss is ready, then wait for
        /// the main hero to walk in. The fight gets its full time after closing one.
        void CloseStuckCinematic()
        {
            var game = CurrentGame;
            if (!cinematicClosed && game.hasCinematic() && elapsed > FightCinematicSeconds)
            {
                cinematicClosed = true;
                Log.Warning("RINGO closing cinematic {c} during the fight", game.curCine?.GetType().Name);
                game.curCine?.destroy();
                Next(Step.Fight);
            }
        }

        void Fight()
        {
            if (walkIn)
            {
                HoldTowardBoss(mainInput!, CurrentGame.hero);
            }
            CloseStuckCinematic();
            Press();
            TrackRemote();
            if (boss!.spr?.groupName?.ToString() is { } anim)
            {
                bossAnims.Add(anim);
            }
            if (elapsed >= nextReport)
            {
                nextReport = elapsed + ReportEvery;
                Log.Information("RINGO t={t:0} boss {x},{y} life={l} aTarget={tgt} | remote {rx},{ry} hits={h} dmg={d} downs={dn} | main {mx},{my} cine={c} fps={fps:0}",
                    elapsed, boss!.cx, boss.cy, boss.life, boss.aTarget?.GetType().Name,
                    Remote.cx, Remote.cy, hitsOnRemote, damageOnRemote, downs, CurrentGame.hero.cx, CurrentGame.hero.cy,
                    CurrentGame.curCine?.GetType().Name, 1 / FrameTime);
            }
            if (boss!.life <= 0 || boss.destroyed)
            {
                Next(Step.AfterDeath);
            }
            else if (elapsed > FightSeconds)
            {
                Log.Information("RINGO boss anims ({who}): {a}", control ? "main" : "remote", string.Join(' ', bossAnims.OrderBy(a => a)));
                if (control || walkIn)
                {
                    Finish();
                    return;
                }
                boss.life = FinishingLife;
                Log.Information("RINGO boss brought down to {l} life", boss.life);
                Next(Step.Finishing);
            }
        }

        void Finishing()
        {
            Press();
            TrackRemote();
            if (boss!.life <= 0 || boss.destroyed)
            {
                Log.Information("RINGO boss died from the remote hero's hit, hits on remote={h} cine={c}",
                    hitsOnRemote, CurrentGame.curCine?.GetType().Name);
                Remote.Input.ReleaseAll();
                Next(Step.AfterDeath);
            }
            else if (elapsed > FinishingSeconds)
            {
                Log.Warning("RINGO boss survived the finish, life={l}", boss.life);
                Remote.Input.ReleaseAll();
                Next(Step.AfterDeath);
            }
        }
    }
}
