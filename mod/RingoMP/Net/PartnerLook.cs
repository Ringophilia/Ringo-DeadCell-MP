using dc.pr;
using HaxeProxy.Runtime;
using ModCore.Utilities;
using Serilog;

namespace RingoMP.Net
{
    /// Who the other players are and what they wear: their names above their heroes,
    /// and their outfits on them, when this player's game has those outfits too.
    public sealed class PartnerLook
    {
        const string NoName = "Player";

        sealed class Worn
        {
            public string Name = "";
            public string Skin = "";
            public string Head = "";
            public RemoteHero? Dressed;
        }

        readonly ILogger log;
        readonly SortedDictionary<int, Worn> peers = [];
        string? toldName;
        string? toldSkin;
        string? toldHead;

        public PartnerLook(ILogger log) => this.log = log;

        /// The first other player's name, for what only ever has one partner.
        public string? PartnerName => peers.Values.FirstOrDefault()?.Name;

        public string? NameOf(int slot) => peers.GetValueOrDefault(slot)?.Name;

        public void ForgetPeer(int slot) => peers.Remove(slot);

        /// The others may have missed what was said before.
        public void NewLink()
        {
            toldName = null;
            toldSkin = null;
            toldHead = null;
        }

        /// Says it again whenever it changes, as players can change outfits in a run.
        public void Send(Action<byte[]> sendReliable)
        {
            var user = Game.Class.ME?.user;
            if (user == null)
            {
                return;
            }
            var ownName = Steamworks.SteamAPI.IsSteamRunning() ? Steamworks.SteamFriends.GetPersonaName() : NoName;
            var ownSkin = user.heroSkin?.ToString() ?? "";
            var ownHead = user.heroHeadSkin?.ToString() ?? "";
            if (ownName == toldName && ownSkin == toldSkin && ownHead == toldHead)
            {
                return;
            }
            (toldName, toldSkin, toldHead) = (ownName, ownSkin, ownHead);
            sendReliable(Packet.Build(PacketType.Look, w =>
            {
                w.Write(ownName);
                w.Write(ownSkin);
                w.Write(ownHead);
            }));
        }

        public void OnLook(int slot, BinaryReader reader)
        {
            var worn = new Worn { Name = reader.ReadString(), Skin = reader.ReadString(), Head = reader.ReadString() };
            peers[slot] = worn;
            log.Information("RINGO player {p} wears {s} with {h}", slot, worn.Skin, worn.Head);
        }

        public void Update(RemoteHero remote)
        {
            if (remote._level == null || !peers.TryGetValue(remote.Slot, out var worn))
            {
                return;
            }
            // The label goes with the hero's graphics, which a new level builds again.
            if (remote.gameplayLabel?.parent == null)
            {
                remote.setGameplayLabel(Translations.Drawable(worn.Name).AsHaxeString(), Ref<int>.Null);
            }
            if (!ReferenceEquals(worn.Dressed, remote))
            {
                worn.Dressed = remote;
                Dress(remote, worn.Skin, worn.Head);
            }
        }

        /// What this player has not unlocked may need assets this game lacks.
        void Dress(RemoteHero remote, string skin, string head)
        {
            var meta = Game.Class.ME?.user?.itemMeta;
            if (meta == null)
            {
                return;
            }
            var wornSkin = skin is { Length: > 0 } && meta.hasUnlockedItem(skin.AsHaxeString()) ? skin : null;
            var wornHead = head is { Length: > 0 } && meta.hasUnlockedItem(head.AsHaxeString()) ? head : null;
            if (wornSkin == null && wornHead == null)
            {
                log.Information("RINGO the partner's outfit is not unlocked here");
                return;
            }
            remote.Outfit = (wornSkin, wornHead);
            try
            {
                remote.Redress();
                log.Information("RINGO the partner's hero now wears {s} with {h}", wornSkin, wornHead);
            }
            catch (Exception e)
            {
                remote.Outfit = null;
                log.Warning("RINGO could not dress the partner's hero: {e}", e.Message);
            }
        }
    }
}
