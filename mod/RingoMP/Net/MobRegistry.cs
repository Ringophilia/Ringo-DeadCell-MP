using System.Security.Cryptography;
using System.Text;
using dc;
using dc.en;
using dc.en.inter;
using dc.pr;

namespace RingoMP.Net
{
    /// Both games built the same level from the same seed, so a mob's place in the
    /// entity list names it on both sides. The signature proves the lists match.
    public sealed class MobRegistry
    {
        const int SignatureBytes = 8;

        readonly List<Mob> mobs = [];
        readonly Dictionary<Mob, ushort> indexes = new(ReferenceEqualityComparer.Instance);
        readonly List<(string Key, Entity Thing)> entries = [];

        public IReadOnlyList<Mob> Mobs => mobs;

        public ulong Signature { get; }

        /// What is gone by now stays out when `skipGone` is set: a level rebuilt to
        /// match the other game's must not count what was just taken out of it.
        public MobRegistry(Level level, bool skipGone = false)
        {
            var entities = level.entities;
            for (var i = 0; i < entities.length; i++)
            {
                if (entities.getDyn(i) is not Entity thing || (skipGone && thing.destroyed))
                {
                    continue;
                }
                if (thing is Mob mob)
                {
                    indexes[mob] = (ushort)mobs.Count;
                    mobs.Add(mob);
                    entries.Add(($"{mob.GetType().Name}:{mob.cx},{mob.cy}", mob));
                }
                // Different unlocks show here first: the item pool shapes what lies around.
                else if (thing is ItemDrop drop && SharedItems.SharedId(drop.item) is { } id)
                {
                    entries.Add(($"{id}:{drop.cx},{drop.cy}", drop));
                }
            }
            DebugText = string.Concat(entries.Select(e => e.Key + ";"));
            var hash = SHA256.HashData(Encoding.ASCII.GetBytes(DebugText));
            Signature = BitConverter.ToUInt64(hash, 0);
        }

        public string DebugText { get; }

        /// Each mob and shared item, in the order the signature counts them.
        public IReadOnlyList<string> Keys => entries.Select(e => e.Key).ToList();

        public bool TryGetIndex(Mob mob, out ushort index) => indexes.TryGetValue(mob, out index);

        public Mob? Get(ushort index) => index < mobs.Count ? mobs[index] : null;

        /// A save the host continued holds its level as it was left, without what
        /// was killed or taken there. When that is all the difference, the rest goes
        /// here too. Anything else means a different level, and nothing is touched.
        public bool TrimTo(IReadOnlyList<string> keep)
        {
            var extra = new List<Entity>();
            var next = 0;
            foreach (var (key, thing) in entries)
            {
                if (next < keep.Count && keep[next] == key)
                {
                    next++;
                }
                else
                {
                    extra.Add(thing);
                }
            }
            if (next < keep.Count)
            {
                return false;
            }
            foreach (var thing in extra)
            {
                thing.destroy();
            }
            return true;
        }

        public static int SignatureSize => SignatureBytes;
    }
}
