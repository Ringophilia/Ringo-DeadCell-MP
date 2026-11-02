using System.Security.Cryptography;
using System.Text;
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

        public IReadOnlyList<Mob> Mobs => mobs;

        public ulong Signature { get; }

        public MobRegistry(Level level)
        {
            var text = new StringBuilder();
            var entities = level.entities;
            for (var i = 0; i < entities.length; i++)
            {
                if (entities.getDyn(i) is Mob mob)
                {
                    indexes[mob] = (ushort)mobs.Count;
                    mobs.Add(mob);
                    text.Append(mob.GetType().Name).Append(':').Append(mob.cx).Append(',').Append(mob.cy).Append(';');
                }
                // Different unlocks show here first: the item pool shapes what lies around.
                else if (entities.getDyn(i) is ItemDrop drop && SharedItems.SharedId(drop.item) is { } id)
                {
                    text.Append(id).Append(':').Append(drop.cx).Append(',').Append(drop.cy).Append(';');
                }
            }
            var hash = SHA256.HashData(Encoding.ASCII.GetBytes(text.ToString()));
            Signature = BitConverter.ToUInt64(hash, 0);
            DebugText = text.ToString();
        }

        public string DebugText { get; }

        public bool TryGetIndex(Mob mob, out ushort index) => indexes.TryGetValue(mob, out index);

        public Mob? Get(ushort index) => index < mobs.Count ? mobs[index] : null;

        public static int SignatureSize => SignatureBytes;
    }
}
