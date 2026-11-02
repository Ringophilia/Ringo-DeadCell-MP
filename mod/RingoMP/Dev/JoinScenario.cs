using dc.cine;
using dc.en.inter;
using dc.pr;
using dc.tool;
using HaxeProxy.Runtime;
using ModCore.Utilities;
using RingoMP.Net;
using Serilog;

namespace RingoMP.Dev
{
    /// Someone joining what is already going on. The host plays alone for two levels,
    /// kills a mob and takes an item, and only then does the client connect
    /// (tools\join.ps1 starts it later). It must end up in the same level, in step,
    /// with the mob gone and the item lying there. The client then picks up gear and
    /// crashes; as RINGO_SECOND_LIFE=1 it starts anew and must get that gear back.
    public sealed class JoinScenario(CoopSession session, NetSession net, ILogger log, string name) : ScriptScenario(session, net, log, name)
    {
        const double JoinWaitSeconds = 120;
        const double SyncSeconds = 3;
        const double ArrivalSeconds = 90;
        const double LoadSeconds = 40;
        const int DropOffsetCells = 2;
        const string GearItem = "StartSword";
        const string ReturningItem = "StartBow";

        readonly bool secondTime = Environment.GetEnvironmentVariable("RINGO_SECOND_LIFE") == "1";

        // Nobody is connected yet when the host starts, and the client is taken along.
        protected override bool CanStart => !IsClient;

        protected override bool CanSpawnRemote => !IsClient || base.CanSpawnRemote;

        protected override IEnumerable<Func<bool>> Run() => IsClient ? ClientRun() : HostRun();

        IEnumerable<Func<bool>> HostRun()
        {
            Log.Information("RINGO Host alone in {id}", CurrentGame.curLevel.map.infos.id.ToString());
            var firstLevel = CurrentGame.curLevel;
            LevelTransition.Class.@goto.Invoke(Doors()[0].destLevel);
            yield return Expect("alone in the second level", () => !ReferenceEquals(CurrentGame.curLevel, firstLevel) && !CurrentGame.hasCinematic(), LoadSeconds);
            var secondLevel = CurrentGame.curLevel;
            LevelTransition.Class.@goto.Invoke(Doors()[0].destLevel);
            yield return Expect("alone in the third level", () => !ReferenceEquals(CurrentGame.curLevel, secondLevel) && !CurrentGame.hasCinematic(), LoadSeconds);
            yield return Seconds(1);

            var mob = Net.Registry!.Mobs.First(m => !m.destroyed && m.life > 0);
            Net.Registry.TryGetIndex(mob, out var index);
            Log.Information("RINGO Host kills mob {i} {type}", index, mob.GetType().Name);
            mob.life = 0;
            mob.kill();
            LeaveAnItem();
            Log.Information("RINGO Host level {id}, killed a mob, left an item, waiting for a player", CurrentGame.curLevel.map.infos.id.ToString());

            yield return Expect("the player arrived in this level", () => Session.Remote != null && Net.LevelsMatch, JoinWaitSeconds);
            Arrived();
            yield return Seconds(SyncSeconds);
            Check("hero state flows", Net.HeroPackets > 0);
            if (secondTime)
            {
                yield break;
            }
            yield return Expect("the player crashed", () => Net.LinkDown || Net.Joins > 1, JoinWaitSeconds);
            yield return Expect("the player came back", () => Net.Connected && Net.Joins > 1 && Session.Remote != null && Net.LevelsMatch, JoinWaitSeconds);
            yield return Seconds(SyncSeconds);
        }

        void LeaveAnItem()
        {
            var item = InventItem.Class.fromItem.Invoke(GearItem.AsHaxeString(), null);
            Main.inventory.add(item);
            Main.dropAndUpdateItem(item);
            new ItemDrop(CurrentGame.curLevel, Main.cx + DropOffsetCells, Main.cy, item, false, Ref<bool>.Null).init();
        }

        IEnumerable<Func<bool>> ClientRun()
        {
            Log.Information("RINGO Client in {id}, seed {s}", CurrentGame.curLevel.map.infos.id.ToString(), Net.Seed);
            yield return Expect("the client reached the host's level", () => Net.LevelsMatch && !CurrentGame.hasCinematic() && Session.Remote != null, ArrivalSeconds);
            Arrived();
            yield return Seconds(SyncSeconds);
            var gone = Net.Registry!.Mobs.Count(m => m.destroyed);
            Log.Information("RINGO Client mobs already gone={g} placed items={p} sword={s}",
                gone, Net.Items.PlacedByPeer, Main.inventory.hasItem(GearItem.AsHaxeString()));
            Check("the mob killed earlier is gone here too", gone >= 1);
            Check("the item left earlier lies here too", Net.Items.PlacedByPeer >= 1);
            Check("mob state flows", Net.MobPackets > 0);
            if (secondTime)
            {
                Check("the gear came back", Main.inventory.hasItem(ReturningItem.AsHaxeString()));
                yield break;
            }
            CoopSession.Give(Main, ReturningItem);
            yield return Seconds(1);
            Log.Information("RINGO Client carrying {n} items, crashing", Main.inventory.items.length);
            Net.SaveLoadoutNow();
            yield return Seconds(1);
            System.Environment.FailFast("RINGO test crash");
        }
    }
}
