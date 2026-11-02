using dc.en;
using dc.en.inter;
using dc.pr;
using dc.tool;
using HaxeProxy.Runtime;
using ModCore.Utilities;
using Serilog;

namespace RingoMP.Net
{
    /// Both games built the same level, so each player already has their own copy of
    /// everything in it. That is the rule for scrolls, cells, gold and what mobs drop.
    /// Equipment lying in the level is the exception: there is one of each, whoever
    /// takes it takes it for both, and what they put down lands in both worlds.
    public sealed class SharedItems
    {
        const int SameDropRangeCells = 3;

        readonly ILogger log;
        readonly Action<PacketType, Action<BinaryWriter>> send;
        readonly HashSet<ItemDrop> shared = new(ReferenceEqualityComparer.Instance);
        readonly HashSet<ItemDrop> removedByPeer = new(ReferenceEqualityComparer.Instance);
        readonly List<(PacketType Type, Action<BinaryWriter> Body)> history = [];
        bool putDownThisFrame;

        public int SharedCount => shared.Count;
        public IReadOnlyCollection<ItemDrop> Shared => shared;
        public int TakenByPeer { get; private set; }
        public int PlacedByPeer { get; private set; }
        public ItemDrop? LastPlacedByPeer { get; private set; }

        public SharedItems(ILogger log, Action<PacketType, Action<BinaryWriter>> send)
        {
            this.log = log;
            this.send = send;
            Hook_ItemDrop.init += OnDropCreated;
            Hook_Hero.dropAndUpdateItem += OnPutDown;
            Hook_Loot.canBePickedUpBy += (orig, self, by) => by is not RemoteHero && orig(self, by);
        }

        public static string? SharedId(InventItem item) => item.kind switch
        {
            InventItemKind.Weapon weapon => weapon.Param0.ToString(),
            InventItemKind.Active active => active.Param0.ToString(),
            InventItemKind.Talisman talisman => talisman.Param0.ToString(),
            _ => null,
        };

        /// Run when a level is ready: what lies around by then came from the level itself.
        public void TrackLevel(Level level)
        {
            Clear();
            var entities = level.entities;
            for (var i = 0; i < entities.length; i++)
            {
                // The armory is each player's own choice of starting gear.
                if (entities.getDyn(i) is ItemDrop { destroyed: false, inArmory: false } drop && SharedId(drop.item) != null)
                {
                    shared.Add(drop);
                }
            }
            log.Information("RINGO {n} shared items in this level", shared.Count);
        }

        /// A level that went away took everything in it, and nobody picked any of that up.
        public void Clear()
        {
            shared.Clear();
            removedByPeer.Clear();
            history.Clear();
        }

        void Tell(PacketType type, Action<BinaryWriter> body)
        {
            history.Add((type, body));
            send(type, body);
        }

        /// For a partner arriving late or back from an outage. Both steps are safe to
        /// repeat: what is already gone is not found, what is there is not placed twice.
        public void Replay()
        {
            foreach (var (type, body) in history)
            {
                send(type, body);
            }
        }

        /// Taking, buying and recycling all end in a destroyed drop, which only gets
        /// disposed of later, so this looks for the flag rather than waiting for that.
        public void Update()
        {
            putDownThisFrame = false;
            shared.RemoveWhere(drop =>
            {
                if (!drop.destroyed)
                {
                    return false;
                }
                if (!removedByPeer.Remove(drop))
                {
                    var id = SharedId(drop.item)!;
                    var (x, y) = (drop.cx, drop.cy);
                    log.Information("RINGO took {id} at {x},{y}", id, x, y);
                    Tell(PacketType.ItemTaken, w =>
                    {
                        w.Write(id);
                        w.Write(x);
                        w.Write(y);
                    });
                }
                return true;
            });
        }

        InventItem OnPutDown(Hook_Hero.orig_dropAndUpdateItem orig, Hero self, InventItem item)
        {
            putDownThisFrame = self is not RemoteHero;
            return orig(self, item);
        }

        /// A swap puts the old item down and creates its drop in one go. Moving
        /// items into the backpack puts them down too, but creates no drop.
        void OnDropCreated(Hook_ItemDrop.orig_init orig, ItemDrop self)
        {
            orig(self);
            if (putDownThisFrame && SharedId(self.item) is { } id)
            {
                shared.Add(self);
                var level = self.item.getRawItemLevel();
                var (x, y) = (self.cx, self.cy);
                Tell(PacketType.ItemPlaced, w =>
                {
                    w.Write(id);
                    w.Write(level);
                    w.Write(x);
                    w.Write(y);
                });
            }
        }

        public void OnTaken(BinaryReader reader)
        {
            var id = reader.ReadString();
            var x = reader.ReadInt32();
            var y = reader.ReadInt32();
            var drop = shared.FirstOrDefault(d => !d.destroyed && SharedId(d.item) == id
                && System.Math.Abs(d.cx - x) <= SameDropRangeCells && System.Math.Abs(d.cy - y) <= SameDropRangeCells);
            log.Information("RINGO partner took {id} at {x},{y}, copy here={f}", id, x, y, drop != null);
            if (drop != null)
            {
                removedByPeer.Add(drop);
                drop.destroy();
                TakenByPeer++;
            }
        }

        public void OnPlaced(BinaryReader reader)
        {
            var id = reader.ReadString();
            var itemLevel = reader.ReadInt32();
            var x = reader.ReadInt32();
            var y = reader.ReadInt32();
            if (shared.Any(d => !d.destroyed && SharedId(d.item) == id && d.cx == x && d.cy == y))
            {
                return;
            }
            var item = InventItem.Class.fromItem.Invoke(id.AsHaxeString(), itemLevel);
            var drop = new ItemDrop(Game.Class.ME.curLevel, x, y, item, false, Ref<bool>.Null);
            drop.init();
            shared.Add(drop);
            LastPlacedByPeer = drop;
            PlacedByPeer++;
        }
    }
}
