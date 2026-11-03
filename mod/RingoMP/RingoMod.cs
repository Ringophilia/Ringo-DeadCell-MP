using dc;
using dc.hxd.snd;
using dc.pr;
using dc.tool;
using ModCore.Events.Interfaces.Game;
using ModCore.Mods;
using RingoMP.Dev;
using RingoMP.Net;

namespace RingoMP
{
    public class RingoMod(ModInfo info) : ModBase(info), IOnFrameUpdate
    {
        const string ScenarioVariable = "RINGO_SCENARIO";
        const string RoleVariable = "RINGO_ROLE";
        const string PortVariable = "RINGO_PORT";
        const string SeedVariable = "RINGO_SEED";
        const string LossVariable = "RINGO_LOSS";
        const string LagVariable = "RINGO_LAG";
        const string StartLateVariable = "RINGO_LATE";
        const string RoomTestVariable = "RINGO_ROOMS";
        const string HostAddress = "127.0.0.1";
        const int DefaultPort = 27815;
        const int TestSeed = 424242;
        const double SnapshotAfterSeconds = 8;

        CoopSession? session;
        NetSession? net;
        IScenario? scenario;
        SteamLobby? lobby;
        Lobby? lobbyMenu;
        PauseLobbyTest? pauseTest;
        double roomTestSeconds = -1;
        string? roomTest;
        bool startLate;

        public override void Initialize()
        {
            session = new CoopSession(Logger);
            if (Environment.GetEnvironmentVariable(ScenarioVariable) is not { Length: > 0 })
            {
                net = new NetSession(session, new IdleTransport(), NetRole.Host, Logger, Random.Shared.Next()) { HostLeadsStart = true };
                lobby = new SteamLobby(Logger, net);
                lobbyMenu = new Lobby(lobby, net, Logger);
                roomTest = Environment.GetEnvironmentVariable(RoomTestVariable);
                if (roomTest == "pause" && Environment.GetEnvironmentVariable("RINGO_SNAPSHOT") is { Length: > 0 } pictures)
                {
                    pauseTest = new PauseLobbyTest(lobbyMenu, Logger, pictures);
                    roomTest = null;
                }
                return;
            }
            // A room forms at the title screen or later, long after the mod has loaded.
            startLate = Environment.GetEnvironmentVariable(StartLateVariable) == "1";
            if (startLate)
            {
                net = new NetSession(session, new IdleTransport(), NetRole.Host, Logger, SeedFromTest()) { HostLeadsStart = true };
            }
            else
            {
                StartTest();
            }
        }

        static float Number(string variable) =>
            float.TryParse(Environment.GetEnvironmentVariable(variable), out var value) ? value : 0;

        static int SeedFromTest() =>
            int.TryParse(Environment.GetEnvironmentVariable(SeedVariable), out var seed) ? seed : TestSeed;

        /// Test games on one machine link over plain IP, through Steam's sockets all the same.
        ITransport CreateTransport(bool isHost, int port)
        {
            var loss = Number(LossVariable);
            var lag = (int)Number(LagVariable);
            return isHost
                ? SteamTransport.HostIp(Logger, (ushort)port, loss, lag, NetSession.MaxPlayers)
                : SteamTransport.JoinIp(Logger, HostAddress, (ushort)port, loss, lag);
        }

        void StartTest()
        {
            if (Environment.GetEnvironmentVariable(RoleVariable) is { Length: > 0 } role)
            {
                var port = int.TryParse(Environment.GetEnvironmentVariable(PortVariable), out var p) ? p : DefaultPort;
                var isHost = role == "host";
                var transport = CreateTransport(isHost, port);
                if (startLate)
                {
                    net!.Attach(transport, isHost ? NetRole.Host : NetRole.Client);
                }
                else
                {
                    net = new NetSession(session!, transport, isHost ? NetRole.Host : NetRole.Client, Logger, SeedFromTest());
                }
            }
            if (Environment.GetEnvironmentVariable(ScenarioVariable) is { Length: > 0 } name)
            {
                scenario = name switch
                {
                    "items" => new ItemScenario(session!, Logger, name),
                    "boss" => new BossScenario(session!, Logger, name),
                    "net" => new NetScenario(session!, net!, Logger, name),
                    "gather" => new GatherScenario(session!, net!, Logger, name),
                    "team" => new TeamScenario(session!, net!, Logger, name),
                    "progress" => new ProgressScenario(session!, net!, Logger, name),
                    "link" => new LinkScenario(session!, net!, Logger, name),
                    "join" => new JoinScenario(session!, net!, Logger, name),
                    "rooms" => new RoomScenario(session!, net!, Logger, name),
                    "modes" => new ModesScenario(session!, net!, Logger, name),
                    "crowd" => new CrowdScenario(session!, net!, Logger, name),
                    _ => new M2Scenario(session!, Logger, name),
                };
                Hook__Save.save += (_, _, _) => { };
                Logger.Information("RINGO scenario {name}: save writes disabled", name);
            }
        }

        /// For tests: nobody can press a key in a game on a hidden desktop.
        void RunRoomTest()
        {
            if (roomTest != null && Main.Class.ME?.curProcess is TitleScreen)
            {
                if (roomTest == "languages")
                {
                    LanguageProbe.Report(Logger);
                }
                lobbyMenu!.Open();
                if (roomTest == "host")
                {
                    // Public, so that the other test game can find it in the list.
                    lobby!.TogglePublic();
                    lobby.Create();
                }
                roomTest = null;
                roomTestSeconds = 0;
            }
        }

        /// For tests: a picture of the lobby once it has settled, as nobody can see a hidden desktop.
        void SnapRoomTest(double dt)
        {
            if (roomTestSeconds < 0 || Environment.GetEnvironmentVariable("RINGO_SNAPSHOT") is not { Length: > 0 } path)
            {
                return;
            }
            roomTestSeconds += dt;
            if (roomTestSeconds >= SnapshotAfterSeconds)
            {
                roomTestSeconds = -1;
                Snapshot.Request(path, Logger);
            }
        }

        void IOnFrameUpdate.OnFrameUpdate(double dt)
        {
            if (startLate && scenario == null && Main.Class.ME?.curProcess is TitleScreen)
            {
                StartTest();
            }
            lobby?.Update();
            RunRoomTest();
            SnapRoomTest(dt);
            pauseTest?.Update(dt);
            lobbyMenu?.Update(dt);
            net?.Update(dt);
            session!.Update();
            if (scenario != null)
            {
                // A test runs out of sight, and must stay out of hearing too.
                Manager.Class.get().masterVolume = 0;
                scenario.Update(dt);
            }
        }
    }
}
