using dc.pr;
using HaxeProxy.Runtime;
using ModCore.Utilities;
using Serilog;

namespace RingoMP.Net
{
    /// Who the partner is and what they wear: their name above their hero, and
    /// their outfit on it, when this player's game has that outfit too.
    public sealed class PartnerLook
    {
        const string NoName = "Player";

        readonly ILogger log;
        string? toldName;
        string? toldSkin;
        string? toldHead;
        string? name;
        string? skin;
        string? head;
        RemoteHero? dressed;

        public PartnerLook(ILogger log) => this.log = log;

        public string? PartnerName => name;

        /// The partner may have missed what was said before.
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

        public void OnLook(BinaryReader reader)
        {
            name = reader.ReadString();
            skin = reader.ReadString();
            head = reader.ReadString();
            dressed = null;
            log.Information("RINGO partner wears {s} with {h}", skin, head);
        }

        public void Update(RemoteHero? remote)
        {
            if (remote?._level == null)
            {
                return;
            }
            // The label goes with the hero's graphics, which a new level builds again.
            if (name != null && remote.gameplayLabel?.parent == null)
            {
                remote.setGameplayLabel(Translations.Drawable(name).AsHaxeString(), Ref<int>.Null);
            }
            if (!ReferenceEquals(dressed, remote) && skin != null)
            {
                dressed = remote;
                Dress(remote);
            }
        }

        /// What this player has not unlocked may need assets this game lacks.
        void Dress(RemoteHero remote)
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
