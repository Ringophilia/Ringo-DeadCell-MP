using dc.en;
using dc.hl.types;
using dc.pr;
using dc.tool;
using ModCore.Utilities;

namespace RingoMP.Net
{
    /// What a hero carries, kept by the host so a player whose game crashed comes
    /// back with it. Scrolls are inventory items too, so this covers the stats,
    /// and each item keeps its affixes, the ones in effect and the ones waiting.
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
                WriteNames(w, item.affixes);
                WriteNames(w, item.inactiveAffixes);
            }
        }

        static void WriteNames(BinaryWriter w, ArrayObj? names)
        {
            var count = names?.length ?? 0;
            w.Write(count);
            for (var i = 0; i < count; i++)
            {
                w.Write(names!.getDyn(i)?.ToString() ?? "");
            }
        }

        static List<string> ReadNames(BinaryReader r)
        {
            var count = r.ReadInt32();
            var names = new List<string>(count);
            for (var i = 0; i < count; i++)
            {
                names.Add(r.ReadString());
            }
            return names;
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
                var affixes = ReadNames(r);
                var waiting = ReadNames(r);
                var item = InventItem.Class.fromItem.Invoke(id.AsHaxeString(), level);
                // The item comes back as it was, not with whatever it was given just now.
                item.clearAffixes();
                foreach (var affix in affixes)
                {
                    var force = true;
                    item.addAffix(affix.AsHaxeString(), ref force);
                }
                foreach (var affix in waiting)
                {
                    item.addInactiveAffix(affix.AsHaxeString());
                }
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
