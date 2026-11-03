using dc;
using dc.tool.bossRush;

namespace RingoMP.Net
{
    public enum RunKind : byte { Normal, Daily, BossRush, Training }

    /// The kind of run the host started, so that the others start the same one. A
    /// continued save is a normal run for them: they start it with the host's seed.
    public readonly record struct RunMode(RunKind Kind, bool Flag = false, bool Long = false, bool Hard = false)
    {
        public static RunMode From(LaunchMode mode) => mode switch
        {
            LaunchMode.NewScoringGame => new(RunKind.Daily),
            LaunchMode.NewBossRushGame rush => new(RunKind.BossRush, rush.Param0, rush.Param1 is BossRushModeLength.Long,
                rush.Param2 is BossRushModeDifficulty.Hard),
            LaunchMode.NewTrainingGame training => new(RunKind.Training, training.Param0),
            _ => new(RunKind.Normal),
        };

        public LaunchMode ToLaunch() => Kind switch
        {
            RunKind.Daily => new LaunchMode.NewScoringGame(),
            RunKind.BossRush => new LaunchMode.NewBossRushGame(Flag,
                Long ? new BossRushModeLength.Long() : new BossRushModeLength.Short(),
                Hard ? new BossRushModeDifficulty.Hard() : new BossRushModeDifficulty.Simple(), null),
            RunKind.Training => new LaunchMode.NewTrainingGame(Flag),
            _ => new LaunchMode.NewGame(false, false),
        };

        public void Write(BinaryWriter w)
        {
            w.Write((byte)Kind);
            w.Write(Flag);
            w.Write(Long);
            w.Write(Hard);
        }

        public static RunMode Read(BinaryReader r) => new((RunKind)r.ReadByte(), r.ReadBoolean(), r.ReadBoolean(), r.ReadBoolean());
    }
}
