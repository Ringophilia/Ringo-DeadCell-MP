using dc;
using dc.h2d;
using dc.hl.types;
using dc.pr;
using dc.ui;
using dc.ui.pause;
using HaxeProxy.Runtime;
using ModCore.Utilities;
using RingoMP.Net;
using Serilog;

namespace RingoMP
{
    /// Where players meet. The title menu's Multiplayer entry opens it, and so does
    /// the one added to the pause menu during a run. Outside a room it lists the
    /// rooms there are; inside one it shows who is there, with the owner's choices.
    public sealed class Lobby
    {
        const double ListRefreshSeconds = 10;

        readonly SteamLobby lobby;
        readonly NetSession net;
        readonly ILogger log;
        readonly bool logPages = Environment.GetEnvironmentVariable("RINGO_PANEL_LOG") == "1";
        readonly Dictionary<DefaultPause, object> pauseEntries = new(ReferenceEqualityComparer.Instance);
        LobbyWindow? window;
        bool openedAtTitle;
        string shown = "";
        double sinceRefresh;
        int menuEntries = -1;

        public Lobby(SteamLobby lobby, NetSession net, ILogger log)
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
                orig(self, Translations.Get(Phrase.MenuMultiplayer).AsHaxeString(), new HlAction(() => OpenAtTitle(self)),
                    Translations.Get(Phrase.MenuMultiplayerHelp).AsHaxeString(), enabled, position);
                log.Information("RINGO title menu has its multiplayer entry");
                return entry;
            };
            Hook_DefaultPause.update += (orig, self) =>
            {
                if (!pauseEntries.ContainsKey(self))
                {
                    pauseEntries[self] = AddPauseEntry(self);
                    LastPause = self;
                }
                orig(self);
            };
        }

        public bool IsOpen => window is { destroyed: false };

        /// For tests: the pause menu seen last, its entry, and the choices a player makes.
        public DefaultPause? LastPause { get; private set; }

        public bool HasPauseEntry(DefaultPause pause) =>
            pauseEntries.TryGetValue(pause, out var entry) && pause.options.indexOf(entry, null) >= 0;

        public void ChoosePauseEntry(DefaultPause pause) => OpenFromPause(pause);

        public void Close() => Back();

        static bool AtTitle => dc.Main.Class.ME?.curProcess is TitleScreen;

        void OnMainMenu(Hook_TitleScreen.orig_mainMenu orig, TitleScreen self)
        {
            menuEntries = 0;
            orig(self);
            menuEntries = -1;
        }

        /// Tests open it, as nobody can press a key in a game on a hidden desktop.
        public void Open()
        {
            if (dc.Main.Class.ME?.curProcess is TitleScreen title)
            {
                OpenAtTitle(title);
            }
        }

        void OpenAtTitle(TitleScreen title)
        {
            if (IsOpen)
            {
                return;
            }
            lobby.Refresh();
            openedAtTitle = true;
            Show(null);
            // As the game does for its options: the title steps back while the window is up.
            title.blur(Ref<double>.Null, Ref<double>.Null);
            title.clearMenu();
        }

        void OpenFromPause(DefaultPause pause)
        {
            if (IsOpen || pause.locked)
            {
                return;
            }
            lobby.Refresh();
            openedAtTitle = false;
            pause.root.visible = false;
            Show(pause);
        }

        void Show(Pause? pause)
        {
            shown = Key(Page());
            window = LobbyWindow.Open(pause, Page);
            sinceRefresh = 0;
            log.Information("RINGO lobby window open, {n} entries", Page().Count);
            LogPage();
        }

        /// The pause menu keeps its choices in a list it walks with the pad and the
        /// keys; this entry goes in that list, just before quitting.
        object AddPauseEntry(DefaultPause pause)
        {
            var options = pause.options;
            var at = System.Math.Max(0, options.length - 1);
            var text = Assets.Class.makeText.Invoke(Translations.Get(Phrase.MenuMultiplayer).AsHaxeString(), null, true, pause.botMenu);
            if (pause.btnQuit is { } quit)
            {
                // Laid out like its neighbour, so that it lines up with the others.
                text.textAlign = quit.textAlign;
                pause.botMenu.addChildAt(text, pause.botMenu.getChildIndex(quit));
            }
            var area = new Interactive(text.textWidth, text.textHeight, text, null);
            dynamic entry = new HaxeDynObj();
            entry.cb = new HlAction(() => OpenFromPause(pause));
            entry.inter = area;
            entry.t = text;
            options.insert(at, entry);
            area.onOver = new HlAction<dc.hxd.Event>(_ => pause.curOptionId = options.indexOf(entry, null));
            area.onClick = new HlAction<dc.hxd.Event>(_ => OpenFromPause(pause));
            // The menu shares its width out between its entries, now one more.
            pause.onResize();
            return entry;
        }

        public void Update(double dt)
        {
            if (window == null)
            {
                return;
            }
            // A run the host started carries the client out of the title, window and all.
            if (window.destroyed || (openedAtTitle && !AtTitle))
            {
                if (!window.destroyed)
                {
                    window.destroy();
                }
                window = null;
                return;
            }
            sinceRefresh += dt;
            if (!lobby.InRoom && sinceRefresh >= ListRefreshSeconds)
            {
                sinceRefresh = 0;
                lobby.Refresh();
            }
            var page = Page();
            var key = Key(page);
            if (key != shown)
            {
                shown = key;
                window.Refresh();
                LogPage();
            }
        }

        static string Key(IReadOnlyList<LobbyEntry> page) => string.Join("\n", page.Select(e => $"{e.Heading}|{e.Label}|{e.Detail}"));

        void LogPage()
        {
            if (logPages)
            {
                log.Information("RINGO lobby page, cursor on {c}:\n{p}", window?.curWidgetId, shown);
            }
        }

        /// What the host starts here, the partner joins by itself.
        void Launch(LaunchMode mode)
        {
            // The title takes its menu back first, so a refused start leaves it as it was.
            window?.onQuit();
            window = null;
            dc.Main.Class.ME.launchGame(mode, null, null);
        }

        void Back() => window?.onQuit();

        IReadOnlyList<LobbyEntry> Page()
        {
            var page = lobby.InRoom ? RoomPage() : ListPage();
            if (lobby.Message is { } message)
            {
                page.Insert(1, new LobbyEntry(Translations.Get(message)));
            }
            page.Add(new LobbyEntry(Translations.Get(Phrase.ActionBack), null, Back));
            return page;
        }

        List<LobbyEntry> ListPage()
        {
            var page = new List<LobbyEntry>
            {
                new(Translations.Get(lobby.Refreshing ? Phrase.ListRefreshing : Phrase.PanelRooms), Heading: true),
            };
            var rooms = lobby.Rooms;
            if (rooms.Count == 0 && !lobby.Refreshing)
            {
                page.Add(new LobbyEntry(Translations.Get(Phrase.ListEmpty)));
            }
            foreach (var room in rooms)
            {
                var state = room.InRun ? Translations.Get(Phrase.ListPlaying, room.Level) : Translations.Get(Phrase.ListWaiting);
                var id = room.Id;
                page.Add(new LobbyEntry($"{Translations.Drawable(room.Name)}   {room.Members}/{room.Capacity}", state, () => lobby.Join(id), Id: $"room {id}"));
            }
            page.Add(new LobbyEntry("", Heading: true));
            page.Add(new LobbyEntry(Translations.Get(Phrase.ActionCreateRoom), Translations.Get(Phrase.ActionCreateRoomHelp), lobby.Create));
            page.Add(new LobbyEntry(Translations.Get(Phrase.ActionRefresh), null, lobby.Refresh));
            page.Add(new LobbyEntry(Translations.Get(lobby.IsPublic ? Phrase.NewRoomsPublic : Phrase.NewRoomsFriends),
                Translations.Get(Phrase.NewRoomsHelp), lobby.TogglePublic, Id: "new rooms"));
            return page;
        }

        List<LobbyEntry> RoomPage()
        {
            var page = new List<LobbyEntry> { new(Translations.Get(Phrase.PanelRoom), Heading: true) };
            var members = lobby.Members();
            foreach (var (name, owner, _) in members)
            {
                var shownName = Translations.Drawable(name);
                page.Add(new LobbyEntry(owner ? Translations.Get(Phrase.MemberHost, shownName) : shownName, owner ? LinkState() : null));
            }
            if (StartHint() is { Length: > 0 } hint)
            {
                page.Add(new LobbyEntry(hint));
            }
            page.Add(new LobbyEntry("", Heading: true));
            if (lobby.IsOwner && AtTitle)
            {
                page.Add(new LobbyEntry(Translations.Get(Phrase.ActionStartRun), null, () => Launch(new LaunchMode.NewGame(false, false))));
                page.Add(new LobbyEntry(Translations.Get(Phrase.ActionContinueRun), null, () => Launch(new LaunchMode.LoadSave())));
                page.Add(new LobbyEntry(Translations.Get(Phrase.ActionDailyRun), Translations.Get(Phrase.ActionDailyRunHelp),
                    () => Launch(new LaunchMode.NewScoringGame())));
            }
            page.Add(new LobbyEntry(Translations.Get(Phrase.ActionInvite), Translations.Get(Phrase.ActionInviteHelp), lobby.Invite));
            if (lobby.IsOwner)
            {
                foreach (var (name, owner, id) in members.Where(m => !m.Owner))
                {
                    page.Add(new LobbyEntry(Translations.Get(Phrase.ActionKickPlayer, Translations.Drawable(name)), null, () => lobby.Kick(id)));
                }
                page.Add(new LobbyEntry(Translations.Get(lobby.Locked ? Phrase.RoomLocked : Phrase.RoomOpen), Translations.Get(Phrase.LockHelp), lobby.ToggleLocked, Id: "lock"));
                page.Add(new LobbyEntry(Translations.Get(Phrase.ActionVisibility,
                    Translations.Get(lobby.IsPublic ? Phrase.VisibilityPublic : Phrase.VisibilityFriends)), Translations.Get(Phrase.VisibilityHelp), lobby.TogglePublic, Id: "visibility"));
            }
            page.Add(new LobbyEntry(Translations.Get(lobby.IsOwner ? Phrase.ActionCloseRoom : Phrase.ActionLeaveRoom), null, lobby.Leave));
            return page;
        }

        string LinkState()
        {
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
            if (!net.Connected)
            {
                return "";
            }
            if (net.InRun)
            {
                return Translations.Get(Phrase.HintLevel, net.LevelId ?? "");
            }
            return Translations.Get(lobby.IsOwner ? Phrase.HintPartnerReady : Phrase.HintWaitingForHost);
        }
    }
}
