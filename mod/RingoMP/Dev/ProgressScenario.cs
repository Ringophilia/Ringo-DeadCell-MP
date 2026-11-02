using dc;
using dc.tool;
using RingoMP.Net;
using Serilog;

namespace RingoMP.Dev
{
    /// Plays two different saves on one: the client pretends to lack every second
    /// unlocked weapon and has fewer boss cells. If the session settles on the lower
    /// progress, the games still build the same levels. With RINGO_PROGRESS=silent
    /// the client keeps that to itself, as a control: the levels must then differ,
    /// which shows the test can see the difference at all.
    public sealed class ProgressScenario : ScriptScenario
    {
        const int LowerBossCells = 0;
        const int LockEvery = 2;

        readonly bool silent = Environment.GetEnvironmentVariable("RINGO_PROGRESS") == "silent";

        public ProgressScenario(CoopSession session, NetSession net, ILogger log, string name) : base(session, net, log, name)
        {
            net.Progress.BeforeSending = Pretend;
        }

        void Pretend(User user)
        {
            if (!IsClient)
            {
                return;
            }
            var weapons = user.itemMeta.getAllUnlockedWeapons();
            var lacking = new List<string>();
            for (var i = 0; i < weapons.length; i += LockEvery)
            {
                lacking.Add(weapons.getDyn(i).ToString()!);
            }
            Log.Information("RINGO client pretends to lack {n} of {t} weapons", lacking.Count, weapons.length);
            Net.Progress.Pretend(lacking, LowerBossCells, silent);
        }

        protected override IEnumerable<Func<bool>> Run()
        {
            Arrived();
            var inSave = dc.Main.Class.ME.user.itemMeta.itemProgress;
            var stillUnlocked = 0;
            for (var i = 0; i < inSave.length; i++)
            {
                if (inSave.getDyn(i) is ItemProgress { unlocked: true })
                {
                    stillUnlocked++;
                }
            }
            Log.Information("RINGO {role} session: boss cells={b} set aside={l} unlocked in the save={s}",
                RoleName, Net.Progress.BossCells, Net.Progress.LockedForSession, stillUnlocked);
            Check("the levels match", Net.LevelsMatch);
            Check("the session uses the lower boss cells", Net.Progress.BossCells == LowerBossCells);
            Check("both games set the same unlocks aside", Net.Progress.LockedForSession > 0);
            yield return Seconds(1);
        }
    }
}
