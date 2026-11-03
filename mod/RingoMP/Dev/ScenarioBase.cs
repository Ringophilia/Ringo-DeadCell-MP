using dc;
using dc.pr;
using Serilog;

namespace RingoMP.Dev
{
    public interface IScenario
    {
        void Update(double dt);
    }

    /// Starts a run by itself, waits for the opening cinematic, spawns the remote
    /// hero, then hands over to the concrete test.
    public abstract class ScenarioBase(CoopSession session, ILogger log, string name) : IScenario
    {
        const double SettleSeconds = 2;
        const double CineTimeoutSeconds = 45;

        enum Phase { Menu, Cinematic, Ready, Playing, Done }

        Phase phase = Phase.Menu;
        double phaseTime;
        double cineWait;
        double total;
        bool finished;

        protected CoopSession Session => session;
        protected ILogger Log => log;
        protected RemoteHero Remote => session.Remote!;
        protected static Game CurrentGame => Game.Class.ME;
        protected double FrameTime { get; private set; }

        /// Holds the start of the run, for a game waiting on the host.
        protected virtual bool CanStart => true;

        /// The run a test starts with.
        protected virtual LaunchMode StartMode => new LaunchMode.NewGame(false, false);

        /// Holds the remote hero back, for a game waiting on the other player.
        protected virtual bool CanSpawnRemote => true;

        /// A test without a partner has its own remote hero; one with a link leaves that to the link.
        protected virtual bool SpawnsRemote => true;

        /// Runs once, right after the remote hero exists.
        protected abstract void OnReady();

        /// Runs every frame until Finish.
        protected abstract void Play();

        /// Hero.toggleFullInvincibility is no substitute: it also stuns every mob in the level.
        protected static void KeepMainAlive()
        {
            if (CurrentGame?.hero is { } hero)
            {
                hero.life = hero.maxLife;
            }
        }

        public void Update(double dt)
        {
            FrameTime = dt;
            phaseTime += dt;
            total += dt;
            try
            {
                Tick();
            }
            catch (Exception e)
            {
                log.Error("RINGO scenario {name} failed: {e}", name, e);
                Finish();
            }
        }

        void Tick()
        {
            switch (phase)
            {
                case Phase.Menu:
                    // The host's game may have started this one.
                    if (Game.Class.ME != null)
                    {
                        Enter(Phase.Cinematic);
                    }
                    else if (Main.Class.ME?.curProcess is TitleScreen && phaseTime > SettleSeconds && CanStart)
                    {
                        Main.Class.ME.launchGame(StartMode, null, null);
                        Enter(Phase.Cinematic);
                    }
                    break;

                case Phase.Cinematic:
                    WaitForCinematic();
                    break;

                case Phase.Ready:
                    Enter(Phase.Playing);
                    OnReady();
                    break;

                case Phase.Playing:
                    Play();
                    break;
            }
        }

        /// Every hero is locked while the opening cinematic plays.
        void WaitForCinematic()
        {
            if (CoopSession.CanSpawn(CurrentGame) && !CurrentGame.hasCinematic())
            {
                if (phaseTime > SettleSeconds && CanSpawnRemote)
                {
                    log.Information("RINGO cinematic over after {t:0}s", cineWait);
                    // A networked game has spawned it by itself, or will once someone joins.
                    if (session.Remote == null && SpawnsRemote)
                    {
                        session.Spawn(CurrentGame);
                    }
                    Enter(Phase.Ready);
                }
                return;
            }
            phaseTime = 0;
            // A popup waits for a key: the daily challenge warns that a modded game saves no score.
            if (dc.ui.ModalPopUp.Class.ME is { destroyed: false, closing: false } popup)
            {
                log.Information("RINGO closing a popup, as a player would");
                popup.close();
            }
            var before = cineWait;
            cineWait += FrameTime;
            // What holds a start up, for a test game nobody can see.
            if ((int)(before / 10) != (int)(cineWait / 10))
            {
                var game = CurrentGame;
                log.Information("RINGO waiting to start: hero={h} awake={a} cx={x} level={l} cinematic={c} cine={n} process={p}",
                    game?.hero != null, game?.hero?.awake, game?.hero?.cx, game?.curLevel != null, game?.hasCinematic(),
                    game?.curCine?.GetType().Name, Main.Class.ME?.curProcess?.GetType().Name);
                if (Environment.GetEnvironmentVariable("RINGO_SNAPSHOT") is { Length: > 0 } pictures)
                {
                    Snapshot.Request(System.IO.Path.Combine(pictures, $"start-{name}-{(int)cineWait}.png"), log);
                }
            }
            if (cineWait > CineTimeoutSeconds && CurrentGame?.curCine != null)
            {
                log.Warning("RINGO cinematic {c} still running, closing it", CurrentGame.curCine.GetType().Name);
                CurrentGame.curCine.destroy();
                cineWait = 0;
            }
        }

        void Enter(Phase next)
        {
            phase = next;
            phaseTime = 0;
        }

        protected void Finish()
        {
            if (!finished)
            {
                finished = true;
                log.Information("RINGO DONE {name} after {t:0}s", name, total);
            }
            phase = Phase.Done;
        }
    }
}
