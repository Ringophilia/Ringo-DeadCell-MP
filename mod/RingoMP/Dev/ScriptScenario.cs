using dc.en;
using dc.en.inter;
using dc.pr;
using RingoMP.Net;
using Serilog;

namespace RingoMP.Dev
{
    /// A test in two games at once, written as a script: each yielded step is a
    /// condition to wait for. Missed checks count, but the run goes on so that the
    /// later steps still report.
    public abstract class ScriptScenario(CoopSession session, NetSession net, ILogger log, string name) : ScenarioBase(session, log, name)
    {
        const double LevelLoadSeconds = 60;
        const double StepSeconds = 12;
        const double FocusSeconds = 1;
        const double TapSeconds = 0.2;

        readonly string role = net.Role.ToString();
        IEnumerator<Func<bool>>? script;
        Func<bool>? waitingFor;
        double now;
        int failures;

        protected NetSession Net => net;
        protected string RoleName => role;
        protected VirtualController Input { get; private set; } = null!;
        protected Level? CurrentLevel { get; private set; }
        protected Hero Main => CurrentGame.hero;
        protected bool IsClient => net.Role == NetRole.Client;

        protected override bool CanStart => net.ReadyToStart;

        protected override bool CanSpawnRemote => session.Remote != null;

        protected override bool SpawnsRemote => false;

        /// The test itself, run in both games.
        protected abstract IEnumerable<Func<bool>> Run();

        protected override void OnReady()
        {
            Input = new VirtualController();
            net.ReadLocalInput = () => Input.HeldMask;
            script = Run().GetEnumerator();
        }

        protected override void Play()
        {
            now += FrameTime;
            Input.Update();
            if (waitingFor != null && !waitingFor())
            {
                return;
            }
            waitingFor = script!.MoveNext() ? script.Current : null;
            if (waitingFor == null)
            {
                Log.Information("RINGO {role} {name} result: failures={f}", role, name, failures);
                Finish();
            }
        }

        protected Func<bool> Seconds(double seconds)
        {
            var until = now + seconds;
            return () => now >= until;
        }

        protected Func<bool> Expect(string check, Func<bool> condition, double seconds = StepSeconds)
        {
            var until = now + seconds;
            return () =>
            {
                if (condition())
                {
                    Log.Information("RINGO {role} CHECK ok: {c}", role, check);
                    return true;
                }
                if (now < until)
                {
                    return false;
                }
                failures++;
                Log.Error("RINGO {role} CHECK FAILED: {c}", role, check);
                return true;
            };
        }

        protected void Check(string what, bool passed)
        {
            if (!passed)
            {
                failures++;
            }
            Log.Information("RINGO {role} CHECK {r}: {c}", role, passed ? "ok" : "FAILED", what);
        }

        protected List<Exit> Doors()
        {
            var entities = CurrentGame.curLevel.entities;
            var doors = new List<Exit>();
            for (var i = 0; i < entities.length; i++)
            {
                // The door a level is entered through is an Exit too, one that leads nowhere.
                if (entities.getDyn(i) is Exit { locked: false, destroyed: false } exit && exit.canBeActivated(Main))
                {
                    doors.Add(exit);
                }
            }
            return doors.OrderBy(d => d.cx).ThenBy(d => d.cy).ToList();
        }

        protected IEnumerable<Func<bool>> PressInteract()
        {
            Input.SetHeld(HeroAction.Interact, true);
            yield return Seconds(TapSeconds);
            Input.SetHeld(HeroAction.Interact, false);
            yield return Seconds(TapSeconds);
        }

        protected IEnumerable<Func<bool>> StandAt(int cx, int cy)
        {
            Main.setPosCase(cx, cy, null, null);
            yield return Seconds(FocusSeconds);
        }

        /// Arrived means both games are in the same new level and past its cinematic.
        protected Func<bool> NewLevel()
        {
            var old = CurrentLevel;
            return Expect("arrived together in a new level", () =>
            {
                // The game is gone on the death screen.
                return CurrentGame is { } game && !ReferenceEquals(game.curLevel, old) && net.LevelsMatch && !game.hasCinematic() && Session.Remote?._level != null;
            }, LevelLoadSeconds);
        }

        protected void Arrived()
        {
            CurrentLevel = CurrentGame.curLevel;
            // The game gives a hero its controller back whenever it places one.
            Main.controller = Input.Access;
            Log.Information("RINGO {role} in {id} mobs={n} doors={d} me {x},{y} remote {rx},{ry}",
                role, CurrentLevel.map?.infos?.id?.ToString(), net.Registry!.Mobs.Count, Doors().Count, Main.cx, Main.cy, Remote.cx, Remote.cy);
        }
    }
}
