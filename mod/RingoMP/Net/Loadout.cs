using dc.en;
using dc.pr;
using dc.tool;
using ModCore.Utilities;

namespace RingoMP.Net
{
    /// What a hero carries, kept by the host so a player whose game crashed comes
    /// back with it. Scrolls are inventory items too, so this covers the stats.
    public static class Loadout
    {
        static string Id(InventItem item) => ((dynamic)item.kind).Param0.ToString();

        public static void Write(BinaryWriter w, Hero hero)
        {
            var items = hero.inventory.items;
            w.Write(hero.cells);
            w.Write(Game.Class.ME.data.money);
            w.Write(items.length);
            for (var i = 0; i < items.length; i++)
            {
                var item = (InventItem)items.getDyn(i);
                w.Write(Id(item));
                w.Write(item.getRawItemLevel());
            }
        }

        public static void Apply(BinaryReader r, Hero hero)
        {
            hero.cells = r.ReadInt32();
            Game.Class.ME.data.money = r.ReadInt32();
            hero.inventory.removeAllEquipments();
            var count = r.ReadInt32();
            for (var i = 0; i < count; i++)
            {
                var id = r.ReadString();
                var level = r.ReadInt32();
                var item = InventItem.Class.fromItem.Invoke(id.AsHaxeString(), level);
                hero.inventory.add(item);
                if (SharedItems.SharedId(item) != null)
                {
                    hero.inventory.equip(item);
                }
            }
            bool updateHud = true, duringInit = false, duringTransform = false;
            hero.onEquipedItemsChange(ref updateHud, ref duringInit, ref duringTransform);
            hero.computeTiers();
            hero.hudInitItems();
        }
    }
}
