using dc.cine;
using dc.tool;
using RingoMP.Net;
using Serilog;

namespace RingoMP.Dev
{
    /// Walks through real doors with real inputs. In the first level the host waits
    /// and cancels both ways, nothing may happen, then both leave. In the next ones
    /// they wait at different doors where there are two, nothing may happen, then
    /// they meet. The third also checks that sync carried over.
    public sealed class GatherScenario(CoopSession session, NetSession net, ILogger log, string name) : ScriptScenario(session, net, log, name)
    {
        const double HoldSeconds = 4;
        const double SyncSeconds = 3;
        const int WalkAwayCells = 8;
        const int FullGatherPeerWaits = 3;

        ExitGathering Gathering => Net.Gathering;

        protected override void OnReady()
        {
            CoopSession.Give(Main, "StartSword");
            CoopSession.Give(Remote, "StartSword");
            base.OnReady();
        }

        protected override void Play()
        {
            if (!IsClient)
            {
                KeepMainAlive();
            }
            base.Play();
        }

        protected override IEnumerable<Func<bool>> Run()
        {
            Arrived();
            foreach (var step in IsClient ? ClientFirstLevel() : HostFirstLevel())
            {
                yield return step;
            }
            yield return NewLevel();
            Arrived();

            // The second level is a corridor between biomes, with one door and no mobs.
            foreach (var step in Meet())
            {
                yield return step;
            }
            yield return NewLevel();
            Arrived();
            MakeSave();

            var heroPackets = Net.HeroPackets;
            var mobPackets = Net.MobPackets;
            yield return Seconds(SyncSeconds);
            Log.Information("RINGO {role} third level: heroPackets+{h} mobPackets+{m} mobErr={e:0.00}",
                RoleName, Net.HeroPackets - heroPackets, Net.MobPackets - mobPackets, Net.LastMobError);
            Check("the level has registered mobs", Net.Registry!.Mobs.Count > 0);
            Check("hero state is arriving", Net.HeroPackets > heroPackets);
            Check("mob state is arriving", !IsClient || Net.MobPackets > mobPackets);

            foreach (var step in Meet())
            {
                yield return step;
            }
            yield return NewLevel();
            Arrived();

            // No door this time: the host alone goes the way a fall would, the client must follow.
            if (!IsClient)
            {
                LevelTransition.Class.@goto.Invoke(Doors()[0].destLevel);
            }
            yield return NewLevel();
            Arrived();
        }

        /// Builds both kinds of save in memory with the remote hero in the world.
        /// Nothing goes to disk: a player's first real save must not be the first try.
        void MakeSave()
        {
            var user = dc.Main.Class.ME.user;
            foreach (var withGame in new[] { false, true })
            {
                var bytes = Save.Class.genSave.Invoke(user, withGame);
                Log.Information("RINGO {role} save withGame={g} bytes={n}", RoleName, withGame, bytes?.length);
                Check($"a save can be built, withGame={withGame}", bytes != null);
            }
        }

        IEnumerable<Func<bool>> HostFirstLevel()
        {
            var door = Doors()[0];
            foreach (var step in StandAt(door.cx, door.cy).Concat(PressInteract()))
            {
                yield return step;
            }
            yield return Expect("door press starts waiting", () => Gathering.LocalWaiting);
            yield return Seconds(HoldSeconds);
            Check("waiting alone does not leave the level", ReferenceEquals(CurrentGame.curLevel, CurrentLevel));

            foreach (var step in PressInteract())
            {
                yield return step;
            }
            yield return Expect("second press cancels", () => !Gathering.LocalWaiting);

            foreach (var step in PressInteract())
            {
                yield return step;
            }
            yield return Expect("door press starts waiting again", () => Gathering.LocalWaiting);
            Main.setPosCase(door.cx - WalkAwayCells, door.cy, null, null);
            yield return Expect("walking away cancels", () => !Gathering.LocalWaiting);
            Check("cancelling does not leave the level", ReferenceEquals(CurrentGame.curLevel, CurrentLevel));

            foreach (var step in StandAt(door.cx, door.cy).Concat(PressInteract()))
            {
                yield return step;
            }
            yield return Expect("waiting a third time", () => Gathering.LocalWaiting);
        }

        IEnumerable<Func<bool>> ClientFirstLevel()
        {
            yield return Expect("saw the host wait, cancel and wait again",
                () => Gathering.PeerWaits >= FullGatherPeerWaits && Gathering.PeerWaiting, 60);
            var door = Doors()[0];
            foreach (var step in StandAt(door.cx, door.cy).Concat(PressInteract()))
            {
                yield return step;
            }
        }

        /// With two doors to choose from, the players first disagree.
        IEnumerable<Func<bool>> Meet()
        {
            var doors = Doors();
            Log.Information("RINGO {role} doors: {d}", RoleName, string.Join(' ', doors.Select(d => $"{d.destLevel}@{d.cx},{d.cy}")));
            var shared = doors[0];
            if (doors.Count < 2)
            {
                foreach (var step in StandAt(shared.cx, shared.cy).Concat(PressInteract()))
                {
                    yield return step;
                }
                yield break;
            }
            var own = IsClient ? doors[1] : shared;
            foreach (var step in StandAt(own.cx, own.cy).Concat(PressInteract()))
            {
                yield return step;
            }
            yield return Expect("both wait", () => Gathering.LocalWaiting && Gathering.PeerWaiting);
            yield return Seconds(HoldSeconds);
            Check("different doors do not leave the level", ReferenceEquals(CurrentGame.curLevel, CurrentLevel));
            if (IsClient)
            {
                foreach (var step in StandAt(shared.cx, shared.cy).Concat(PressInteract()))
                {
                    yield return step;
                }
            }
        }
    }
}
