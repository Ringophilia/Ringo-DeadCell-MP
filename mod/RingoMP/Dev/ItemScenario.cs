using dc.en;
using Serilog;

namespace RingoMP.Dev
{
    /// Equips every item on a hero, uses it on a mob, records the outcome.
    /// A crash ends the process, so the id goes to disk before the item is touched
    /// and tools\items.ps1 restarts the game after the last finished line.
    /// With RINGO_SUBJECT=main the local hero is tested instead, as a control:
    /// a crash that happens there too has nothing to do with the remote hero.
    public sealed class ItemScenario(CoopSession session, ILogger log, string name) : ScenarioBase(session, log, name)
    {
        const double ItemSeconds = 1.3;
        const double PulseSeconds = 0.2;
        const double RepositionSeconds = 0.5;
        const int MobApproachCells = 1;

        readonly string currentFile = Environment.GetEnvironmentVariable("RINGO_CURRENT")!;
        readonly string resultsFile = Environment.GetEnvironmentVariable("RINGO_RESULTS")!;
        readonly bool onMain = Environment.GetEnvironmentVariable("RINGO_SUBJECT") == "main";
        readonly List<string[]> pending = [];

        Hero subject = null!;
        VirtualController input = null!;
        Mob? mob;
        int index = -1;
        double itemTime;
        double repositionTime;
        int dealt;
        int lastMobLife;

        protected override void OnReady()
        {
            if (onMain)
            {
                subject = CurrentGame.hero;
                input = new VirtualController();
                subject.controller = input.Access;
            }
            else
            {
                subject = Remote;
                input = Remote.Input;
            }
            var done = File.Exists(resultsFile)
                ? File.ReadAllLines(resultsFile).Select(l => l.Split('\t')[0]).ToHashSet()
                : [];
            foreach (var line in File.ReadAllLines(Environment.GetEnvironmentVariable("RINGO_ITEMS")!))
            {
                var fields = line.Split('\t');
                if (!done.Contains(fields[0]))
                {
                    pending.Add(fields);
                }
            }
            Log.Information("RINGO {n} items to test on {who}", pending.Count, onMain ? "main" : "remote");
            NextItem();
        }

        protected override void Play()
        {
            itemTime += FrameTime;
            repositionTime += FrameTime;
            if (onMain)
            {
                input.Update();
            }
            KeepTargetAlive();
            KeepMainAlive();
            subject.life = subject.maxLife;
            if (repositionTime > RepositionSeconds)
            {
                repositionTime = 0;
                MoveNextToMob();
            }

            var pressed = (int)(itemTime / PulseSeconds) % 2 == 0;
            input.SetHeld(HeroAction.Attack1, pressed);
            input.SetHeld(HeroAction.Attack2, pressed);
            input.SetHeld(HeroAction.Skill1, pressed);
            input.SetHeld(HeroAction.Skill2, pressed);

            if (itemTime > ItemSeconds)
            {
                var used = subject.weaponsManager.lastWeaponUsed?.GetType().Name ?? "-";
                Record(used);
                NextItem();
            }
        }

        void MoveNextToMob()
        {
            if (mob != null)
            {
                subject.setPosCase(mob.cx - MobApproachCells, mob.cy, null, null);
                input.SetHeld(HeroAction.Right, true);
            }
        }

        void KeepTargetAlive()
        {
            if (mob == null || mob.destroyed || mob.life <= 0)
            {
                dealt += lastMobLife;
                mob = NearestMob();
                lastMobLife = mob?.life ?? 0;
                MoveNextToMob();
                return;
            }
            if (mob.life < lastMobLife)
            {
                dealt += lastMobLife - mob.life;
            }
            mob.life = mob.maxLife;
            lastMobLife = mob.life;
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
                    var distance = System.Math.Abs(m.cx - subject.cx) + System.Math.Abs(m.cy - subject.cy);
                    if (distance < bestDistance)
                    {
                        best = m;
                        bestDistance = distance;
                    }
                }
            }
            return best;
        }

        void NextItem()
        {
            index++;
            if (index >= pending.Count)
            {
                Finish();
                return;
            }
            var id = pending[index][0];
            File.WriteAllText(currentFile, id);
            input.ReleaseAll();
            subject.inventory.removeAllEquipments();
            itemTime = 0;
            dealt = 0;
            try
            {
                CoopSession.Give(subject, id);
                subject.refillAllAmmo();
                MoveNextToMob();
            }
            catch (Exception e)
            {
                Record("GIVE-ERROR " + e.Message.Split('\n')[0]);
                NextItem();
            }
        }

        void Record(string outcome) =>
            File.AppendAllText(resultsFile, $"{pending[index][0]}\t{pending[index][1]}\tok\t{dealt}\t{outcome}\n");
    }
}
