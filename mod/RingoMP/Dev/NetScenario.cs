using dc.en;
using RingoMP.Net;
using Serilog;

namespace RingoMP.Dev
{
    /// Runs in two games at once. The client walks, both players attack one mob
    /// from either side, then the client stands by another mob without fighting
    /// back. Each side logs what it sees, and tools\net.ps1 prints both.
    public sealed class NetScenario(CoopSession session, NetSession net, ILogger log, string name) : ScenarioBase(session, log, name)
    {
        const double SettleSeconds = 3;
        const double WalkSeconds = 2;
        const double FightSeconds = 25;
        const double TankSeconds = 14;
        const double CoolDownSeconds = 3;
        const double ReportEvery = 1;
        const double AttackPulse = 0.3;
        const double ApproachEvery = 1.2;
        const int MobApproachCells = 1;
        const string MeleeMob = "Zombie";

        enum Step { Settle, Walk, Fight, Tank, CoolDown }

        readonly string role = net.Role.ToString();
        Step step;
        double elapsed;
        double nextReport;
        double nextApproach;
        VirtualController input = null!;
        Mob? mob;
        int mobIndex = -1;
        int spawnX, spawnY;

        Hero Main => CurrentGame.hero;

        bool IsClient => net.Role == NetRole.Client;

        protected override bool CanStart => net.ReadyToStart;

        protected override bool CanSpawnRemote => net.LevelsMatch;

        protected override void OnReady()
        {
            CoopSession.Give(Main, "StartSword");
            CoopSession.Give(Remote, "StartSword");
            spawnX = Main.cx;
            spawnY = Main.cy;
            input = new VirtualController();
            Main.controller = input.Access;
            net.ReadLocalInput = () => input.HeldMask;
            Next(Step.Settle);
        }

        void Next(Step next)
        {
            step = next;
            elapsed = 0;
            nextReport = 0;
            nextApproach = 0;
            Log.Information("RINGO {role} --- {step}", role, next);
        }

        protected override void Play()
        {
            elapsed += FrameTime;
            input.Update();
            if (!IsClient)
            {
                KeepMainAlive();
            }
            if (elapsed >= nextReport)
            {
                nextReport = elapsed + ReportEvery;
                Report();
            }
            switch (step)
            {
                case Step.Settle:
                    if (elapsed > SettleSeconds)
                    {
                        input.SetHeld(HeroAction.Right, IsClient);
                        Next(Step.Walk);
                    }
                    break;

                case Step.Walk:
                    if (elapsed > WalkSeconds)
                    {
                        input.ReleaseAll();
                        PickMob();
                        Next(Step.Fight);
                    }
                    break;

                case Step.Fight:
                    Fight();
                    break;

                case Step.Tank:
                    Tank();
                    break;

                case Step.CoolDown:
                    if (elapsed > CoolDownSeconds)
                    {
                        Log.Information("RINGO {role} result: client life={l} dmgFromHost={d} hitsSent={h} animsSent={s} animsPlayed={p}",
                            role, Main.life, net.DamageTakenFromHost, net.HitsSent, net.AnimsSent, net.AnimsPlayed);
                        Finish();
                    }
                    break;
            }
        }

        /// Both games know the same mobs and the same spawn point, so both pick the same one.
        void PickMob(string? type = null)
        {
            var mobs = net.Registry!.Mobs;
            mob = null;
            mobIndex = -1;
            var bestDistance = int.MaxValue;
            for (var i = 0; i < mobs.Count; i++)
            {
                var m = mobs[i];
                if (m.life > 0 && !m.destroyed && (type == null || m.GetType().Name == type))
                {
                    var distance = System.Math.Abs(m.cx - spawnX) + System.Math.Abs(m.cy - spawnY);
                    if (distance < bestDistance)
                    {
                        mob = m;
                        mobIndex = i;
                        bestDistance = distance;
                    }
                }
            }
            Log.Information("RINGO {role} picked mob {i} {type}", role, mobIndex, mob?.GetType().Name);
        }

        void Approach(int side)
        {
            if (elapsed >= nextApproach && mob != null)
            {
                nextApproach = elapsed + ApproachEvery;
                Main.setPosCase(mob.cx + side * MobApproachCells, mob.cy, null, null);
            }
        }

        void Fight()
        {
            var side = IsClient ? -1 : 1;
            Approach(side);
            var pressed = (int)(elapsed / AttackPulse) % 2 == 0;
            input.SetHeld(HeroAction.Attack1, pressed);
            input.SetHeld(HeroAction.Attack2, pressed);
            input.SetHeld(HeroAction.Right, side < 0);
            input.SetHeld(HeroAction.Left, side > 0);
            var dead = mob == null || mob.destroyed || mob.life <= 0;
            if (elapsed > FightSeconds || dead)
            {
                input.ReleaseAll();
                Log.Information("RINGO {role} fight over mob={i} dead={d} life={l} after {t:0.0}s", role, mobIndex, dead, mob?.life, elapsed);
                PickMob(MeleeMob);
                if (!IsClient)
                {
                    Main.set_targetable(false);
                }
                Next(Step.Tank);
            }
        }

        void Tank()
        {
            if (IsClient)
            {
                Approach(-1);
            }
            if (elapsed > TankSeconds)
            {
                input.ReleaseAll();
                Next(Step.CoolDown);
            }
        }

        void Report()
        {
            var remote = Remote;
            Log.Information("RINGO {role} {step} me {x:0.0},{y:0.0} life={l} peer {px:0.0},{py:0.0} life={pl} | mob#{i} {mx:0.0},{my:0.0} life={ml} anim={an} target={t} | hitsSent={hs} dmg={d} mobErr={e:0.00}",
                role, step, Main.cx + Main.xr, Main.cy + Main.yr, Main.life,
                remote.cx + remote.xr, remote.cy + remote.yr, remote.life,
                mobIndex, mob == null ? 0 : mob.cx + mob.xr, mob == null ? 0 : mob.cy + mob.yr, mob?.life, mob?.spr?.groupName?.ToString(),
                mob?.aTarget?.GetType().Name, net.HitsSent, net.DamageTakenFromHost, net.LastMobError);
        }
    }
}
