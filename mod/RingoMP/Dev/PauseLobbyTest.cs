using dc;
using dc.pr;
using dc.tool;
using dc.ui.pause;
using Serilog;

namespace RingoMP.Dev
{
    /// For tests: starts a run, opens the pause menu, then the lobby from its
    /// Multiplayer entry, then goes back, taking a picture at each step. Nobody can
    /// press a key in a game on a hidden desktop, or see what it shows.
    public sealed class PauseLobbyTest
    {
        const double SettleSeconds = 3;

        readonly Lobby lobby;
        readonly ILogger log;
        readonly string pictures;
        readonly IEnumerator<double> steps;
        double wait;
        int failures;

        public PauseLobbyTest(Lobby lobby, ILogger log, string pictures)
        {
            this.lobby = lobby;
            this.log = log;
            this.pictures = pictures;
            steps = Run().GetEnumerator();
            // The run is a throwaway, and the save must not hear of it.
            Hook__Save.save += (_, _, _) => { };
        }

        public void Update(double dt)
        {
            wait -= dt;
            if (wait <= 0 && steps.MoveNext())
            {
                wait = steps.Current;
            }
        }

        void Check(string what, bool ok)
        {
            if (!ok)
            {
                failures++;
            }
            log.Information("RINGO pause CHECK {r}: {w}", ok ? "ok" : "FAILED", what);
        }

        void Picture(string name) => Snapshot.Request(System.IO.Path.Combine(pictures, $"pause-{name}.png"), log);

        IEnumerable<double> Run()
        {
            while (Main.Class.ME?.curProcess is not TitleScreen)
            {
                yield return 1;
            }
            Main.Class.ME.launchGame(new LaunchMode.NewGame(false, false), null, null);
            while (!CoopSession.CanSpawn(dc.pr.Game.Class.ME) || dc.pr.Game.Class.ME.hasCinematic())
            {
                yield return 1;
            }
            yield return SettleSeconds;

            // What the pause button calls; the proxy keeps it out of reach.
            typeof(dc.pr.Game).GetMethod("pauseGameIfAvailable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(dc.pr.Game.Class.ME, null);
            yield return SettleSeconds;
            var pause = lobby.LastPause;
            Check("the pause menu is open", pause is { destroyed: false });
            if (pause == null)
            {
                log.Information("RINGO pause result: failures={f}", failures);
                yield break;
            }
            Check("the pause menu has the multiplayer entry", lobby.HasPauseEntry(pause));
            Picture("menu");
            yield return 1;

            lobby.ChoosePauseEntry(pause);
            yield return SettleSeconds;
            Check("the lobby opened over the pause menu", lobby.IsOpen && !pause.root.visible);
            Picture("lobby");
            yield return 1;

            lobby.Close();
            yield return SettleSeconds;
            Check("back in the pause menu", !lobby.IsOpen && pause.root.visible && !pause.destroyed);
            Picture("back");
            yield return 1;
            log.Information("RINGO pause result: failures={f}", failures);
        }
    }
}
