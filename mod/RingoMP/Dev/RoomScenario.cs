using dc;
using dc.cine;
using dc.en.inter;
using dc.pr;
using RingoMP.Net;
using Serilog;

namespace RingoMP.Dev
{
    /// Side rooms: the host walks on, the client following, until a level has a door
    /// into one. Both go in through it together and must be in step in there, then
    /// both come back out and must be in step in the level again, with its mobs as
    /// they were. Each kind of room door in the level gets its turn, two at most.
    public sealed class RoomScenario(CoopSession session, NetSession net, ILogger log, string name) : ScriptScenario(session, net, log, name)
    {
        const int MaxLevels = 6;
        const double SyncSeconds = 3;
        const double RoomSeconds = 30;
        const int MaxDoorKinds = 2;

        protected override void Play()
        {
            KeepMainAlive();
            base.Play();
        }

        /// The ways into side rooms and back out, the same list in both games.
        List<Entity> RoomDoors()
        {
            var entities = CurrentGame.curLevel.entities;
            var doors = new List<Entity>();
            for (var i = 0; i < entities.length; i++)
            {
                if (entities.getDyn(i) is Entity { destroyed: false } door && door is ZDoor or Portal or EntranceTeleporter
                    && door.canBeActivated(Main))
                {
                    doors.Add(door);
                }
            }
            return doors.OrderBy(d => d.cx).ThenBy(d => d.cy).ToList();
        }

        void LogRooms(string when)
        {
            var rooms = CurrentGame.subLevels;
            var names = new List<string>();
            for (var i = 0; rooms != null && i < rooms.length; i++)
            {
                var room = rooms.getDyn(i) as Level;
                var mark = ReferenceEquals(room, CurrentGame.curLevel) ? "*" : "";
                names.Add($"{mark}{room?.map?.infos?.id}");
            }
            Log.Information("RINGO {role} {w}: side room={s} rooms [{r}] doors {d}", RoleName, when, Net.InSideRoom,
                string.Join(' ', names), string.Join(' ', RoomDoors().Select(d => $"{d.GetType().Name}@{d.cx},{d.cy}")));
        }

        protected override IEnumerable<Func<bool>> Run()
        {
            Arrived();
            var levels = 1;
            while (RoomDoors().Count == 0 && levels < MaxLevels)
            {
                LogRooms("no room door");
                if (!IsClient)
                {
                    LevelTransition.Class.@goto.Invoke(Doors()[0].destLevel);
                }
                yield return NewLevel();
                Arrived();
                levels++;
            }
            LogRooms("level");
            var doors = RoomDoors();
            if (doors.Count == 0)
            {
                Check("found a level with a side room", false);
                yield break;
            }
            foreach (var door in doors.GroupBy(d => d.GetType()).Select(g => g.First()).Take(MaxDoorKinds).ToList())
            {
                foreach (var step in ThroughRoom(door))
                {
                    yield return step;
                }
            }

            // On to the next level, which must count as new again.
            if (!IsClient)
            {
                LevelTransition.Class.@goto.Invoke(Doors()[0].destLevel);
            }
            yield return NewLevel();
            Arrived();
            Check("the next level is not a side room", !Net.InSideRoom);
        }

        IEnumerable<Func<bool>> ThroughRoom(Entity door)
        {
            var level = CurrentGame.curLevel;
            var mobs = Net.Registry!.Mobs.Count;
            Log.Information("RINGO {role} going through the {t} at {x},{y}", RoleName, door.GetType().Name, door.cx, door.cy);
            foreach (var step in StandAt(door.cx, door.cy).Concat(PressInteract()))
            {
                yield return step;
            }
            yield return Expect("both went into the side room together", () => Net.InSideRoom && Net.LevelsMatch && !CurrentGame.hasCinematic()
                && Remote._level != null && ReferenceEquals(Remote._level, CurrentGame.curLevel), RoomSeconds);
            Arrived();
            LogRooms("inside");
            var heroPackets = Net.HeroPackets;
            yield return Seconds(SyncSeconds);
            Check("hero state flows in the side room", Net.HeroPackets > heroPackets);

            var ways = RoomDoors();
            if (ways.Count == 0)
            {
                Check("the side room has a way back", false);
                yield break;
            }
            var back = ways.OrderBy(d => System.Math.Abs(d.cx - Main.cx) + System.Math.Abs(d.cy - Main.cy)).First();
            foreach (var step in StandAt(back.cx, back.cy).Concat(PressInteract()))
            {
                yield return step;
            }
            yield return Expect("both came back out together", () => !Net.InSideRoom && Net.LevelsMatch && !CurrentGame.hasCinematic()
                && ReferenceEquals(CurrentGame.curLevel, level) && ReferenceEquals(Remote._level, level), RoomSeconds);
            Arrived();
            LogRooms("back");
            Check("the level kept its mobs", Net.Registry!.Mobs.Count == mobs);
            heroPackets = Net.HeroPackets;
            var mobPackets = Net.MobPackets;
            yield return Seconds(SyncSeconds);
            Check("hero state flows back in the level", Net.HeroPackets > heroPackets);
            Check("mob state flows back in the level", !IsClient || Net.MobPackets > mobPackets);
        }
    }
}
