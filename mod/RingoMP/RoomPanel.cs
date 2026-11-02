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
                var entry = orig(self, T("单人游戏", "Single player").AsHaxeString(), callback, help, enabled, position);
                orig(self, T("多人游戏", "Multiplayer").AsHaxeString(), new HlAction(Open),
                    T("创建或加入房间，和朋友一起玩", "Create or join a room and play with a friend").AsHaxeString(), enabled, position);
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

        static string T(string chinese, string english) =>
            Lang.Class.LANG?.ToString().StartsWith("zh") == true ? chinese : english;

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
                return T("已连接", "Connected");
            }
            if (net.LinkDown)
            {
                return T("连接中断，正在重连…", "Link lost, reconnecting…");
            }
            return lobby.IsOwner ? T("等待玩家加入", "Waiting for a player") : T("正在连接房主…", "Connecting to the host…");
        }

        string StartHint()
        {
            if (!lobby.InRoom || !net.Connected)
            {
                return "";
            }
            if (net.InRun)
            {
                return T($"当前关卡：{net.LevelId}", $"Level: {net.LevelId}");
            }
            return lobby.IsOwner
                ? T("队友已就绪，可以开始游戏", "Your partner is ready, you can start")
                : T("等待房主开始游戏", "Waiting for the host to start");
        }

        IEnumerable<string> RoomLines()
        {
            yield return T("联机房间", "Co-op room");
            yield return LinkState();
            yield return "";
            foreach (var (name, owner, _) in lobby.Members())
            {
                yield return owner ? T($"  {name}（房主）", $"  {name} (host)") : $"  {name}";
            }
            yield return "";
            yield return StartHint();
            yield return "";
            if (lobby.IsOwner)
            {
                yield return T(lobby.Locked ? "房间：已锁定" : "房间：开放", lobby.Locked ? "Room: locked" : "Room: open")
                    + "    " + T(lobby.IsPublic ? "所有人可见" : "仅好友可见", lobby.IsPublic ? "public" : "friends only");
                if (AtTitle)
                {
                    yield return T("[S] 开始新游戏  [C] 继续存档", "[S] New run  [C] Continue save");
                }
                yield return T("[I] 邀请好友  [K] 移出玩家  [L] 锁定/解锁", "[I] Invite  [K] Remove player  [L] Lock/unlock");
                yield return T("[P] 切换可见范围  [X] 关闭房间  [Esc] 返回", "[P] Visibility  [X] Close room  [Esc] Back");
            }
            else
            {
                yield return T("[I] 邀请好友  [X] 退出房间  [Esc] 返回", "[I] Invite  [X] Leave room  [Esc] Back");
            }
        }

        IEnumerable<string> ListLines()
        {
            yield return T("联机房间列表", "Co-op rooms");
            yield return lobby.Refreshing ? T("正在刷新…", "Refreshing…") : "";
            var rooms = lobby.Rooms;
            if (rooms.Count == 0 && !lobby.Refreshing)
            {
                yield return T("  没有找到房间", "  No rooms found");
            }
            for (var i = 0; i < System.Math.Min(rooms.Count, ListedRooms); i++)
            {
                var room = rooms[i];
                var state = room.InRun ? T($"游戏中 {room.Level}", $"playing {room.Level}") : T("等待中", "waiting");
                yield return $"  [{i + 1}] {room.Name}  {room.Members}/{room.Capacity}  {state}";
            }
            yield return "";
            yield return T(lobby.IsPublic ? "新房间：所有人可见" : "新房间：仅好友可见", lobby.IsPublic ? "New rooms: public" : "New rooms: friends only");
            yield return T("[N] 新建房间  [R] 刷新  [P] 切换可见范围  [Esc] 返回", "[N] New room  [R] Refresh  [P] Visibility  [Esc] Back");
        }

        string Content()
        {
            var lines = (lobby.InRoom ? RoomLines() : ListLines()).ToList();
            if (lobby.Message is { } message)
            {
                lines.Add(T(message.Chinese, message.English));
            }
            return string.Join('\n', lines);
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
