using RingoMP.Net;
using Serilog;

namespace RingoMP.Dev
{
    /// Three or four games at once. Everyone arrives in the same level and sees
    /// everyone else, through the host for those who joined. The host then waits at
    /// a door while the others come one by one, and nobody leaves before the last
    /// is there, twice. In the third level the last one to join leaves the session: the
    /// others see them go, the mobs get weaker, and the rest meet at a door again.
    public sealed class CrowdScenario(CoopSession session, NetSession net, ILogger log, string name) : ScriptScenario(session, net, log, name)
    {
        const string PlayersVariable = "RINGO_PLAYERS";
        const double ArriveSeconds = 90;
        const double StaggerSeconds = 6;
        const double HoldSeconds = 4;
        const int NearCells = 3;
        const double JoinStaggerSeconds = 4;
        // Waiting at a door one after the other takes a while in a level full of mobs.
        const int TestLife = 100000;

        readonly int players = int.TryParse(Environment.GetEnvironmentVariable(PlayersVariable), out var count) ? count : 3;

        bool staggered;
        // Mobs knock a waiting hero about, and three cells away from its door it stops waiting.
        (int X, int Y, dc.pr.Level Level)? pin;

        // The others are taken along, as players are.
        protected override bool CanStart
        {
            get
            {
                if (!staggered)
                {
                    staggered = true;
                    Net.JoinRunDelay = () => JoinStaggerSeconds * Net.Slot;
                }
                return StartsRun;
            }
        }

        bool StartsRun
            => !IsClient && Net.Players >= players && base.CanStart;

        protected override bool CanSpawnRemote => Session.Remotes.Count >= players - 1;

        bool Everyone(int expected) => Net.Players == expected && Session.Remotes.Count == expected - 1
            && Session.Remotes.Values.All(r => r._level != null && !r.Away);

        bool Leaver => Net.Slot == players - 1;

        protected override void Play()
        {
            Main.maxLife = TestLife;
            KeepMainAlive();
            if (pin is { } at && ReferenceEquals(at.Level, CurrentGame.curLevel) && (Main.cx != at.X || Main.cy != at.Y))
            {
                Main.setPosCase(at.X, at.Y, null, null);
            }
            base.Play();
        }

        protected override IEnumerable<Func<bool>> Run()
        {
            Arrived();
            yield return Expect("everyone is in the level", () => Everyone(players), ArriveSeconds);
            Log.Information("RINGO {role} is player {p} of {n}, sees {r}", RoleName, Net.Slot, Net.Players, string.Join(',', Session.Remotes.Keys));
            Check("this player has a number of its own", IsClient ? Net.Slot > 0 : Net.Slot == 0);
            yield return Expect("every other player said who they are", () => Session.Remotes.Keys.All(s => Net.Look.NameOf(s) != null));

            // Everyone stands at the door, and must be seen there by everyone else.
            var door = Doors()[0];
            foreach (var step in StandAt(door.cx, door.cy))
            {
                yield return step;
            }
            yield return Expect("every other player was seen at the door", () => Session.Remotes.Values.All(r =>
                System.Math.Abs(r.cx - door.cx) <= NearCells && System.Math.Abs(r.cy - door.cy) <= NearCells), 15);

            foreach (var step in Gather(door, players))
            {
                yield return step;
            }
            yield return NewLevel();
            Arrived();
            yield return Expect("everyone arrived in the next level", () => Everyone(players), ArriveSeconds);

            // The second level is a corridor without mobs, so everyone goes on to the third.
            door = Doors()[0];
            foreach (var step in StandAt(door.cx, door.cy).Concat(Gather(door, players)))
            {
                yield return step;
            }
            yield return NewLevel();
            Arrived();
            yield return Expect("everyone arrived in the third level", () => Everyone(players), ArriveSeconds);

            var mob = Net.Registry!.Mobs.FirstOrDefault(m => !m.destroyed && m.life > 0);
            var mobLife = mob?.maxLife ?? 0;
            if (Leaver)
            {
                yield return Seconds(HoldSeconds);
                Log.Information("RINGO {role} leaves the session", RoleName);
                Net.Leave();
                yield return Expect("alone after leaving", () => Net.Players == 1 && Session.Remotes.Count == 0);
                yield break;
            }
            yield return Expect("the player who left is gone", () => Everyone(players - 1), 30);
            if (!IsClient && mob != null)
            {
                Log.Information("RINGO {role} mob life {a} -> {b}", RoleName, mobLife, mob.maxLife);
                Check("the mobs are weaker for fewer players", mob.maxLife < mobLife);
            }

            door = Doors()[0];
            foreach (var step in StandAt(door.cx, door.cy).Concat(Gather(door, players - 1)))
            {
                yield return step;
            }
            yield return NewLevel();
            Arrived();
            yield return Expect("the rest arrived together", () => Everyone(players - 1), ArriveSeconds);
        }

        /// The host goes first and the others follow one by one; the door opens for the last.
        IEnumerable<Func<bool>> Gather(dc.en.inter.Exit door, int present)
        {
            yield return Seconds(StaggerSeconds * Net.Slot);
            pin = (door.cx, door.cy, CurrentGame.curLevel);
            foreach (var step in StandAt(door.cx, door.cy).Concat(PressInteract()))
            {
                yield return step;
            }
            if (Net.Slot < present - 1)
            {
                yield return Expect("waiting at the door", () => Net.Gathering.LocalWaiting);
                yield return Seconds(HoldSeconds);
                Check("nobody leaves before everyone is there", ReferenceEquals(CurrentGame.curLevel, CurrentLevel));
            }
        }
    }
}
