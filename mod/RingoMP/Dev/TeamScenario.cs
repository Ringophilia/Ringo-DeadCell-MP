using dc;
using dc.en.inter;
using dc.pr;
using dc.tool;
using HaxeProxy.Runtime;
using ModCore.Utilities;
using RingoMP.Net;
using Serilog;

namespace RingoMP.Dev
{
    /// What two players share and owe each other. One takes an item off the ground
    /// and it leaves the other world. The client goes down and is rescued, goes down
    /// again and comes back in the next level. Then both go down and the run ends.
    public sealed class TeamScenario(CoopSession session, NetSession net, ILogger log, string name) : ScriptScenario(session, net, log, name)
    {
        const double RescueHoldSeconds = 2;
        const double RescueWindowSeconds = 31;
        const double AfterRunSeconds = 15;
        const double NextRunSeconds = 60;
        const double ItemHuntSeconds = 30;
        const double LandSeconds = 2;
        const int DropOffsetCells = 2;
        const string DroppedItem = "StartSword";

        TeamLife Life => Net.Life;

        protected override IEnumerable<Func<bool>> Run()
        {
            Arrived();
            Log.Information("RINGO {role} first mob life={l} shared items={s}", RoleName, Net.Registry!.Mobs[0].life, Net.Items.SharedCount);
            foreach (var step in TakeAnItem().Concat(Rescue()).Concat(NextLevelReturn()).Concat(Wipe()))
            {
                yield return step;
            }
        }

        /// The host puts a sword down the way a swap does, the client picks it up.
        IEnumerable<Func<bool>> TakeAnItem()
        {
            if (!IsClient)
            {
                var item = InventItem.Class.fromItem.Invoke(DroppedItem.AsHaxeString(), null);
                Main.inventory.add(item);
                Main.dropAndUpdateItem(item);
                var before = Net.Items.SharedCount;
                new ItemDrop(CurrentGame.curLevel, Main.cx + DropOffsetCells, Main.cy, item, false, Ref<bool>.Null).init();
                Check("the drop is shared", Net.Items.SharedCount == before + 1);
                yield return Expect("the partner took it from this world too", () => Net.Items.TakenByPeer == 1, ItemHuntSeconds);
                yield break;
            }
            yield return Expect("the sword landed in this world too", () => Net.Items.PlacedByPeer == 1, ItemHuntSeconds);
            var drop = Net.Items.LastPlacedByPeer!;
            // A fresh drop lands first and only then can be picked up.
            yield return Seconds(LandSeconds);
            foreach (var step in StandAt(drop.cx, drop.cy))
            {
                yield return step;
            }
            // Items with a long press read the raw device, which a virtual pad does not
            // have, so this calls what the key press ends in.
            Check("the client can reach it", drop.canBeActivated(Main));
            drop.onActivate(Main, false);
            yield return Expect("the client picked it up", () => drop.destroyed);
            Check("the client carries the sword", Main.inventory.hasItem(DroppedItem.AsHaxeString()));
        }

        IEnumerable<Func<bool>> Rescue()
        {
            if (IsClient)
            {
                Life.GoDown();
                Check("the client is down", Life.Local == DownState.Rescuable);
                yield return Expect("the client stood up", () => Life.Local == DownState.Standing, 20);
                Check("standing up gives half the life back", Main.life == Main.maxLife / 2);
                yield break;
            }
            yield return Expect("the host saw the client go down", () => Life.Peer == DownState.Rescuable && Remote.Downed, ItemHuntSeconds);
            foreach (var step in StandAt(Remote.cx, Remote.cy))
            {
                yield return step;
            }
            Input.SetHeld(HeroAction.Interact, true);
            yield return Seconds(RescueHoldSeconds);
            Input.SetHeld(HeroAction.Interact, false);
            yield return Expect("the host saw the client stand up", () => Life.Peer == DownState.Standing && !Remote.Downed);
        }

        IEnumerable<Func<bool>> NextLevelReturn()
        {
            yield return Seconds(1);
            if (IsClient)
            {
                Life.GoDown();
                yield return Expect("too late for a rescue", () => Life.Local == DownState.WaitingForNextLevel, RescueWindowSeconds + 2);
            }
            else
            {
                yield return Expect("the host saw the window close", () => Life.Peer == DownState.WaitingForNextLevel, RescueWindowSeconds + 4);
                var door = Doors()[0];
                foreach (var step in StandAt(door.cx, door.cy).Concat(PressInteract()))
                {
                    yield return step;
                }
            }
            yield return NewLevel();
            Arrived();
            Check("back on their feet in the next level", Life.Local == DownState.Standing);
            yield return Expect("the partner is back too", () => Life.Peer == DownState.Standing && !Remote.Downed);
        }

        IEnumerable<Func<bool>> Wipe()
        {
            yield return Seconds(1);
            Life.GoDown();
            yield return Expect("the hero died", () => Life.Wiped && Main.life <= 0);
            yield return Seconds(AfterRunSeconds);
            // The game then waits on its death screen for a key press.
            Log.Information("RINGO {role} after the wipe: cine={c} life={l} process={p}",
                RoleName, CurrentGame?.curCine?.GetType().Name, CurrentGame?.hero?.life, dc.Main.Class.ME?.curProcess?.GetType().Name);
            if (!Net.HostLeadsStart)
            {
                yield break;
            }
            // The next run starts together too: what the host starts, the client joins.
            var oldGame = CurrentGame;
            if (!IsClient)
            {
                dc.Main.Class.ME.launchGame(new LaunchMode.NewGame(false, false), null, null);
            }
            yield return Expect("a new run with both players", () => !ReferenceEquals(CurrentGame, oldGame) && Session.Remote != null && Net.LevelsMatch, NextRunSeconds);
        }
    }
}
