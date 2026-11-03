using dc;
using dc.h2d;
using dc.hl.types;
using dc.hxd;
using dc.pr;
using HaxeProxy.Runtime;
using ModCore.Utilities;
using RingoMP.Net;
using Serilog;

namespace RingoMP
{
    /// The lobby. The title menu's Multiplayer entry opens it, and F5 brings it back
    /// during a run. Outside a room it lists the rooms there are; inside one it shows
    /// who is there, with the owner's controls. Letters and digits rather than a
    /// cursor: during a run the hero is listening to the arrow keys.
    public sealed class RoomPanel
    {
        const int ToggleKey = 116;
        const int CloseKey = 27;
        const int StartKey = 83;
        const int ContinueKey = 67;
        const int NewRoomKey = 78;
        const int RefreshKey = 82;
        const int InviteKey = 73;
        const int KickKey = 75;
        const int LockKey = 76;
        const int PublicKey = 80;
        const int LeaveKey = 88;
        const int FirstRoomKey = 49;
        const int ListedRooms = 9;
        const int Margin = 24;
        const int Padding = 16;
        const int MinPanelWidth = 320;
        const int TextColor = 0xFFFFFF;
        const int BackgroundColor = 0x101018;
        const double BackgroundAlpha = 0.85;

        readonly SteamLobby lobby;
        readonly NetSession net;
        readonly ILogger log;
        readonly bool logLines = Environment.GetEnvironmentVariable("RINGO_PANEL_LOG") == "1";
        Bitmap? background;
        Text? text;
        string shown = "";
        bool open;
        int menuEntries = -1;

        public RoomPanel(SteamLobby lobby, NetSession net, ILogger log)
        {
            this.lobby = lobby;
            this.net = net;
            this.log = log;
            Hook_TitleScreen.mainMenu += OnMainMenu;
            // The main menu's first entry plays alone, and the lobby comes right after it.
            Hook_TitleScreen.addMenu += (orig, self, label, callback, help, enabled, position) =>
            {
                if (menuEntries != 0)
                {
                    return orig(self, label, callback, help, enabled, position);
                }
                menuEntries = 1;
                var entry = orig(self, Translations.Get(Phrase.MenuSinglePlayer).AsHaxeString(), callback, help, enabled, position);
                orig(self, Translations.Get(Phrase.MenuMultiplayer).AsHaxeString(), new HlAction(Open),
                    Translations.Get(Phrase.MenuMultiplayerHelp).AsHaxeString(), enabled, position);
                log.Information("RINGO title menu has its multiplayer entry");
                return entry;
            };
            Hook_TitleScreen.update += OnTitleUpdate;
        }

        bool AtTitle => dc.Main.Class.ME?.curProcess is TitleScreen;

        void OnMainMenu(Hook_TitleScreen.orig_mainMenu orig, TitleScreen self)
        {
            menuEntries = 0;
            orig(self);
            menuEntries = -1;
        }

        /// The title menu must not react to what is typed into the lobby on top of it.
        void OnTitleUpdate(Hook_TitleScreen.orig_update orig, TitleScreen self)
        {
            if (!open)
            {
                orig(self);
            }
        }

        /// Tests open it, as nobody can press a key in a game on a hidden desktop.
        public void Open()
        {
            open = true;
            lobby.Refresh();
        }

        public void Update()
        {
            if (Pressed(ToggleKey) || (open && Pressed(CloseKey)))
            {
                if (open)
                {
                    open = false;
                }
                else
                {
                    Open();
                }
            }
            else if (open)
            {
                ReadKeys();
            }
            Draw();
        }

        /// What the host starts here, the partner joins by itself.
        void Launch(LaunchMode mode)
        {
            open = false;
            dc.Main.Class.ME.launchGame(mode, null, null);
        }

        static bool Pressed(int key) => Key.Class.isPressed.Invoke(key);

        void ReadKeys()
        {
            if (lobby.InRoom)
            {
                if (Pressed(InviteKey))
                {
                    lobby.Invite();
                }
                if (Pressed(KickKey))
                {
                    lobby.KickPartner();
                }
                if (Pressed(LockKey))
                {
                    lobby.ToggleLocked();
                }
                if (Pressed(PublicKey))
                {
                    lobby.TogglePublic();
                }
                if (Pressed(LeaveKey))
                {
                    lobby.Leave();
                }
                if (AtTitle && lobby.IsOwner && Pressed(StartKey))
                {
                    Launch(new LaunchMode.NewGame(false, false));
                }
                if (AtTitle && lobby.IsOwner && Pressed(ContinueKey))
                {
                    Launch(new LaunchMode.LoadSave());
                }
                return;
            }
            if (Pressed(NewRoomKey))
            {
                lobby.Create();
            }
            if (Pressed(RefreshKey))
            {
                lobby.Refresh();
            }
            if (Pressed(PublicKey))
            {
                lobby.TogglePublic();
            }
            var rooms = lobby.Rooms;
            for (var i = 0; i < System.Math.Min(rooms.Count, ListedRooms); i++)
            {
                if (Pressed(FirstRoomKey + i))
                {
                    lobby.Join(rooms[i].Id);
                }
            }
        }

        string LinkState()
        {
            if (!lobby.InRoom)
            {
                return "";
            }
            if (net.Connected)
            {
                return Translations.Get(Phrase.StateConnected);
            }
            if (net.LinkDown)
            {
                return Translations.Get(Phrase.StateReconnecting);
            }
            return Translations.Get(lobby.IsOwner ? Phrase.StateWaitingForPlayer : Phrase.StateConnecting);
        }

        string StartHint()
        {
            if (!lobby.InRoom || !net.Connected)
            {
                return "";
            }
            if (net.InRun)
            {
                return Translations.Get(Phrase.HintLevel, net.LevelId ?? "");
            }
            return Translations.Get(lobby.IsOwner ? Phrase.HintPartnerReady : Phrase.HintWaitingForHost);
        }

        IEnumerable<string> RoomLines()
        {
            yield return Translations.Get(Phrase.PanelRoom);
            yield return LinkState();
            yield return "";
            foreach (var (name, owner, _) in lobby.Members())
            {
                var shown = Translations.Drawable(name);
                yield return "  " + (owner ? Translations.Get(Phrase.MemberHost, shown) : shown);
            }
            yield return "";
            yield return StartHint();
            yield return "";
            if (lobby.IsOwner)
            {
                yield return Translations.Get(lobby.Locked ? Phrase.RoomLocked : Phrase.RoomOpen)
                    + "    " + Translations.Get(lobby.IsPublic ? Phrase.VisibilityPublic : Phrase.VisibilityFriends);
                if (AtTitle)
                {
                    yield return Translations.Get(Phrase.KeysStart);
                }
                yield return Translations.Get(Phrase.KeysOwner);
                yield return Translations.Get(Phrase.KeysOwnerMore);
            }
            else
            {
                yield return Translations.Get(Phrase.KeysMember);
            }
        }

        IEnumerable<string> ListLines()
        {
            yield return Translations.Get(Phrase.PanelRooms);
            yield return lobby.Refreshing ? Translations.Get(Phrase.ListRefreshing) : "";
            var rooms = lobby.Rooms;
            if (rooms.Count == 0 && !lobby.Refreshing)
            {
                yield return "  " + Translations.Get(Phrase.ListEmpty);
            }
            for (var i = 0; i < System.Math.Min(rooms.Count, ListedRooms); i++)
            {
                var room = rooms[i];
                var state = room.InRun ? Translations.Get(Phrase.ListPlaying, room.Level) : Translations.Get(Phrase.ListWaiting);
                yield return $"  [{i + 1}] {Translations.Drawable(room.Name)}  {room.Members}/{room.Capacity}  {state}";
            }
            yield return "";
            yield return Translations.Get(lobby.IsPublic ? Phrase.NewRoomsPublic : Phrase.NewRoomsFriends);
            yield return Translations.Get(Phrase.KeysList);
        }

        string Content()
        {
            var lines = (lobby.InRoom ? RoomLines() : ListLines()).ToList();
            if (lobby.Message is { } message)
            {
                lines.Add(Translations.Get(message));
            }
            return string.Join("\n", lines);
        }

        void Build()
        {
            var scene = Boot.Class.ME.s2d;
            var tile = Tile.Class.fromColor.Invoke(BackgroundColor, 1, 1, BackgroundAlpha, null);
            background = new Bitmap(tile, scene);
            background.x = Margin;
            background.y = Margin;
            text = new Text(Assets.Class.font18, scene);
            text.textColor = TextColor;
            text.x = Margin + Padding;
            text.y = Margin + Padding;
        }

        void Draw()
        {
            if (background == null)
            {
                if (!open)
                {
                    return;
                }
                Build();
            }
            background!.visible = open;
            text!.visible = open;
            if (!open)
            {
                return;
            }
            var content = Content();
            if (content != shown)
            {
                shown = content;
                text.text = content.AsHaxeString();
                // The text decides the size: languages and room lists differ a lot in length.
                background.scaleX = System.Math.Max(text.textWidth + 2 * Padding, MinPanelWidth);
                background.scaleY = text.textHeight + 2 * Padding;
                if (logLines)
                {
                    log.Information("RINGO panel:\n{c}", content);
                }
            }
        }
    }
}
