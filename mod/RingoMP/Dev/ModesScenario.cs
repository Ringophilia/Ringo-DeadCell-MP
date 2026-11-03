using dc;
using dc.pr;
using dc.tool.bossRush;
using RingoMP.Net;
using Serilog;

namespace RingoMP.Dev
{
    /// Other kinds of run: the host starts the daily challenge from the title and the
    /// client must end up in the same one, with scores kept off the leaderboard. The
    /// host then starts Boss Rush from inside the run, as its door does, and the
    /// client must follow into that too.
    public sealed class ModesScenario(CoopSession session, NetSession net, ILogger log, string name) : ScriptScenario(session, net, log, name)
    {
        const double ModeSeconds = 120;

        protected override LaunchMode StartMode => new LaunchMode.NewScoringGame();

        // The client is taken along, as players are.
        protected override bool CanStart => !IsClient && base.CanStart;

        protected override void Play()
        {
            KeepMainAlive();
            base.Play();
        }

        protected override IEnumerable<Func<bool>> Run()
        {
            Arrived();
            Log.Information("RINGO {role} run seed {s} mode {m} scoring={sc}", RoleName, CurrentGame.data.gameSeed, Net.Mode.Kind, CurrentGame.isScoring());
            Check("the run is the daily challenge", CurrentGame.isScoring());
            Check("the levels match", Net.LevelsMatch);
            Check("the run is known to be played together", Net.PlayedTogether);

            var daily = CurrentGame;
            if (!IsClient)
            {
                dc.Main.Class.ME.launchGame(new LaunchMode.NewBossRushGame(false, new BossRushModeLength.Short(), new BossRushModeDifficulty.Simple(), null), null, null);
            }
            yield return Expect("both are in Boss Rush together", () =>
            {
                var game = CurrentGame;
                return game != null && !ReferenceEquals(game, daily) && game.isBossRush() && Net.LevelsMatch && !game.hasCinematic()
                    && Session.Remote?._level != null;
            }, ModeSeconds);
            Arrived();
            Log.Information("RINGO {role} run seed {s} mode {m}", RoleName, CurrentGame.data.gameSeed, Net.Mode.Kind);
            Check("the client heard it is Boss Rush", Net.Mode.Kind == RunKind.BossRush);
            var heroPackets = Net.HeroPackets;
            yield return Seconds(3);
            Check("hero state flows in Boss Rush", Net.HeroPackets > heroPackets);
        }
    }
}
