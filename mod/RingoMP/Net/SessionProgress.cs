using dc;
using dc.pr;
using dc.tool;
using Serilog;

namespace RingoMP.Net
{
    /// A run is played at the level of whoever has less, because what a level
    /// generates depends on what is unlocked and both games must build the same one.
    /// Only reads are narrowed: the save stays as it is, and whatever a run unlocks
    /// goes into it the usual way, so each player keeps what they earned.
    /// A run that is already going cannot change, so someone joining it takes over
    /// its progress as it stands, whichever way that differs from their own.
    public sealed class SessionProgress
    {
        readonly ILogger log;
        readonly HashSet<string> unlockedHere = [];
        readonly HashSet<string> lockedForSession = [];
        readonly HashSet<string> pretendLacking = [];
        readonly HashSet<string> grantedByRun = [];
        HashSet<string>? peerUnlocked;
        int? peerBossCells;
        int? sessionBossCells;
        int? pretendedBossCells;
        int ownBossCells;
        bool silentPretence;
        bool sent;

        public bool Ready => peerBossCells != null && sent;
        public int LockedForSession => lockedForSession.Count;
        public int BossCells { get; private set; }

        /// This player is joining a run in progress.
        public bool JoiningRun { get; set; }

        /// Runs right before this player's progress goes out.
        public Action<User>? BeforeSending { get; set; }

        public SessionProgress(ILogger log)
        {
            this.log = log;
            Hook_ItemMetaManager.hasUnlockedItem += OnHasUnlockedItem;
            Hook_User.br_numActivated += OnBossCells;
        }

        bool OnHasUnlockedItem(Hook_ItemMetaManager.orig_hasUnlockedItem orig, ItemMetaManager self, dc.String id)
        {
            if (lockedForSession.Count == 0 && grantedByRun.Count == 0)
            {
                return orig(self, id);
            }
            var key = id.ToString();
            return grantedByRun.Contains(key) || (orig(self, id) && !lockedForSession.Contains(key));
        }

        int OnBossCells(Hook_User.orig_br_numActivated orig, User self) =>
            sessionBossCells ?? pretendedBossCells ?? orig(self);

        /// For tests on one save: behaves like a player who lacks these. A silent
        /// pretence leaves the partner uninformed, so the two games must drift apart.
        public void Pretend(IEnumerable<string> lacking, int bossCells, bool silent)
        {
            pretendedBossCells = bossCells;
            pretendLacking.UnionWith(lacking);
            lockedForSession.UnionWith(lacking);
            silentPretence = silent;
        }

        /// A new partner, or the old one back: each side says again where it stands.
        public void NewLink()
        {
            sent = false;
            peerBossCells = null;
            peerUnlocked = null;
        }

        /// Sent before a run can start, or to whoever joins one. Before a run the game
        /// has no save loaded for itself, so this reads its own copy.
        public void Send(Action<byte[]> send)
        {
            var main = Main.Class.ME;
            var running = main?.user != null && Game.Class.ME != null;
            if (sent || (!running && main?.curProcess is not TitleScreen))
            {
                return;
            }
            sent = true;
            var user = running ? main!.user : Save.Class.tryLoad.Invoke() ?? new User();
            BeforeSending?.Invoke(user);
            unlockedHere.Clear();
            var progress = user.itemMeta.itemProgress;
            for (var i = 0; i < progress.length; i++)
            {
                if (progress.getDyn(i) is ItemProgress { unlocked: true } item)
                {
                    unlockedHere.Add(item.itemId.ToString());
                }
            }
            var told = silentPretence ? unlockedHere : unlockedHere.Except(pretendLacking).Except(lockedForSession).ToHashSet();
            ownBossCells = user.br_numActivated();
            send(Packet.Build(PacketType.Progress, w =>
            {
                w.Write(ownBossCells);
                w.Write(told.Count);
                foreach (var id in told)
                {
                    w.Write(id);
                }
            }));
            Settle();
        }

        public void OnPeerProgress(BinaryReader reader)
        {
            peerBossCells = reader.ReadInt32();
            var count = reader.ReadInt32();
            peerUnlocked = new HashSet<string>(count);
            for (var i = 0; i < count; i++)
            {
                peerUnlocked.Add(reader.ReadString());
            }
            Settle();
        }

        void Settle()
        {
            if (peerUnlocked == null || !sent)
            {
                return;
            }
            if (JoiningRun)
            {
                lockedForSession.Clear();
                lockedForSession.UnionWith(unlockedHere.Except(peerUnlocked));
                grantedByRun.UnionWith(peerUnlocked.Except(unlockedHere));
                sessionBossCells = peerBossCells;
            }
            else if (Game.Class.ME == null)
            {
                lockedForSession.UnionWith(unlockedHere.Except(peerUnlocked));
                sessionBossCells = System.Math.Min(ownBossCells, peerBossCells!.Value);
            }
            BossCells = sessionBossCells ?? ownBossCells;
            log.Information("RINGO session progress: {b} boss cells, {l} of {u} unlocks set aside, {g} granted",
                BossCells, lockedForSession.Count, unlockedHere.Count, grantedByRun.Count);
        }
    }
}
