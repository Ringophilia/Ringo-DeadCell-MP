using Serilog;
using Steamworks;

namespace RingoMP.Net
{
    /// One room in the list.
    public sealed record RoomInfo(CSteamID Id, string Name, int Members, int Capacity, bool InRun, string Level, bool Open);

    /// Rooms are Steam lobbies. They only introduce the players: everything after
    /// that runs over a direct link to the room's owner.
    public sealed class SteamLobby
    {
        const int Capacity = 2;
        const int MaxListedRooms = 50;
        const double RefreshFriendsAfterSeconds = 0;
        const string MarkKey = "ringomp";
        const string MarkValue = "1";
        const string NameKey = "name";
        const string RunKey = "run";
        const string LevelKey = "level";
        const string ConnectKey = "connect";
        const string ConnectArgument = "+connect_lobby";

        readonly ILogger log;
        readonly NetSession net;
        readonly Callback<GameLobbyJoinRequested_t> joinRequested;
        readonly Callback<LobbyDataUpdate_t> dataUpdated;
        readonly Callback<LobbyChatUpdate_t> membersChanged;
        readonly CallResult<LobbyCreated_t> created;
        readonly CallResult<LobbyEnter_t> entered;
        readonly CallResult<LobbyMatchList_t> listed;
        readonly Dictionary<ulong, RoomInfo> rooms = [];
        readonly HashSet<ulong> waitingForData = [];
        readonly HashSet<ulong> kicked = [];
        CSteamID lobby = CSteamID.Nil;
        CSteamID joinAtStart = CSteamID.Nil;
        ELobbyType visibility = ELobbyType.k_ELobbyTypeFriendsOnly;
        bool locked;
        bool busy;
        string toldRun = "";
        string toldLevel = "";

        public IReadOnlyList<RoomInfo> Rooms => rooms.Values.OrderByDescending(r => r.Open).ThenBy(r => r.Name).ToList();
        public bool InRoom => lobby != CSteamID.Nil;
        public bool IsOwner => InRoom && SteamMatchmaking.GetLobbyOwner(lobby) == SteamUser.GetSteamID();
        public bool Locked => locked;
        public bool IsPublic => visibility == ELobbyType.k_ELobbyTypePublic;
        public bool Refreshing { get; private set; }

        /// The last thing that went wrong or worth telling, in Chinese and English.
        public (string Chinese, string English)? Message { get; private set; }

        public SteamLobby(ILogger log, NetSession net)
        {
            this.log = log;
            this.net = net;
            joinRequested = Callback<GameLobbyJoinRequested_t>.Create(r => Join(r.m_steamIDLobby));
            dataUpdated = Callback<LobbyDataUpdate_t>.Create(OnDataUpdated);
            membersChanged = Callback<LobbyChatUpdate_t>.Create(OnMembersChanged);
            created = CallResult<LobbyCreated_t>.Create(OnCreated);
            entered = CallResult<LobbyEnter_t>.Create(OnEntered);
            listed = CallResult<LobbyMatchList_t>.Create(OnListed);
            net.Refused = (chinese, english) => Message = (chinese, english);
            net.KickedOut = OnKicked;
            ReadLaunchArguments();
        }

        // DCCM's own host only sees itself in .NET's view of the command line.
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern IntPtr GetCommandLineW();

        /// Steam starts the game with this when a friend accepts an invite or uses
        /// "Join Game" while the game is closed.
        void ReadLaunchArguments()
        {
            var arguments = System.Runtime.InteropServices.Marshal.PtrToStringUni(GetCommandLineW())!
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var at = Array.IndexOf(arguments, ConnectArgument);
            if (at >= 0 && at + 1 < arguments.Length && ulong.TryParse(arguments[at + 1], out var id))
            {
                log.Information("RINGO started to join lobby {id}", id);
                joinAtStart = new CSteamID(id);
            }
        }

        public void Update()
        {
            SteamAPI.RunCallbacks();
            if (joinAtStart != CSteamID.Nil && dc.Main.Class.ME?.curProcess is dc.pr.TitleScreen)
            {
                var target = joinAtStart;
                joinAtStart = CSteamID.Nil;
                Join(target);
            }
            if (InRoom && IsOwner)
            {
                PublishState();
            }
        }

        void PublishState()
        {
            var run = net.InRun ? "1" : "0";
            var level = net.LevelId ?? "";
            if (run != toldRun || level != toldLevel)
            {
                toldRun = run;
                toldLevel = level;
                SteamMatchmaking.SetLobbyData(lobby, RunKey, run);
                SteamMatchmaking.SetLobbyData(lobby, LevelKey, level);
            }
        }

        public void Create()
        {
            if (busy || InRoom)
            {
                return;
            }
            busy = true;
            Message = null;
            log.Information("RINGO creating a room");
            created.Set(SteamMatchmaking.CreateLobby(visibility, Capacity));
        }

        void OnCreated(LobbyCreated_t result, bool failed)
        {
            busy = false;
            if (failed || result.m_eResult != EResult.k_EResultOK)
            {
                log.Warning("RINGO no room: {r}", result.m_eResult);
                Message = ("创建房间失败", "Could not create a room");
                return;
            }
            lobby = new CSteamID(result.m_ulSteamIDLobby);
            log.Information("RINGO room {id} is open", lobby.m_SteamID);
            SteamMatchmaking.SetLobbyData(lobby, MarkKey, MarkValue);
            SteamMatchmaking.SetLobbyData(lobby, NameKey, $"{SteamFriends.GetPersonaName()}");
            SteamFriends.SetRichPresence(ConnectKey, $"{ConnectArgument} {lobby.m_SteamID}");
            net.Attach(SteamTransport.HostP2P(log, MayConnect), NetRole.Host);
        }

        public void Refresh()
        {
            if (Refreshing)
            {
                return;
            }
            Refreshing = true;
            Message = null;
            rooms.Clear();
            waitingForData.Clear();
            FindFriendsRooms();
            SteamMatchmaking.AddRequestLobbyListStringFilter(MarkKey, MarkValue, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(MaxListedRooms);
            listed.Set(SteamMatchmaking.RequestLobbyList());
        }

        /// Friends-only rooms never show in the public list, but friends' status names them.
        void FindFriendsRooms()
        {
            var flags = EFriendFlags.k_EFriendFlagImmediate;
            for (var i = 0; i < SteamFriends.GetFriendCount(flags); i++)
            {
                var friend = SteamFriends.GetFriendByIndex(i, flags);
                if (SteamFriends.GetFriendGamePlayed(friend, out var game) && game.m_steamIDLobby.IsValid()
                    && game.m_gameID.AppID() == SteamUtils.GetAppID())
                {
                    waitingForData.Add(game.m_steamIDLobby.m_SteamID);
                    SteamMatchmaking.RequestLobbyData(game.m_steamIDLobby);
                }
            }
        }

        void OnListed(LobbyMatchList_t result, bool failed)
        {
            Refreshing = false;
            for (var i = 0; !failed && i < result.m_nLobbiesMatching; i++)
            {
                AddRoom(SteamMatchmaking.GetLobbyByIndex(i));
            }
            log.Information("RINGO {n} rooms listed", rooms.Count);
        }

        void OnDataUpdated(LobbyDataUpdate_t update)
        {
            if (waitingForData.Remove(update.m_ulSteamIDLobby) && update.m_bSuccess != 0)
            {
                AddRoom(new CSteamID(update.m_ulSteamIDLobby));
            }
        }

        void AddRoom(CSteamID id)
        {
            if (SteamMatchmaking.GetLobbyData(id, MarkKey) != MarkValue)
            {
                return;
            }
            var members = SteamMatchmaking.GetNumLobbyMembers(id);
            var capacity = SteamMatchmaking.GetLobbyMemberLimit(id);
            rooms[id.m_SteamID] = new RoomInfo(id, SteamMatchmaking.GetLobbyData(id, NameKey), members, capacity,
                SteamMatchmaking.GetLobbyData(id, RunKey) == "1", SteamMatchmaking.GetLobbyData(id, LevelKey), members < capacity);
        }

        public void Join(CSteamID room)
        {
            if (busy || room == lobby)
            {
                return;
            }
            if (InRoom)
            {
                Leave();
            }
            busy = true;
            Message = null;
            log.Information("RINGO joining room {id}", room.m_SteamID);
            entered.Set(SteamMatchmaking.JoinLobby(room));
        }

        void OnEntered(LobbyEnter_t result, bool failed)
        {
            busy = false;
            if (failed || result.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                log.Warning("RINGO could not enter the room: {r}", result.m_EChatRoomEnterResponse);
                Message = ("无法加入：房间已满、已锁定或已关闭", "Could not join: the room is full, locked or closed");
                return;
            }
            lobby = new CSteamID(result.m_ulSteamIDLobby);
            net.Attach(SteamTransport.JoinP2P(log, SteamMatchmaking.GetLobbyOwner(lobby)), NetRole.Client);
        }

        void OnMembersChanged(LobbyChatUpdate_t change) =>
            log.Information("RINGO room members changed: {n}", InRoom ? SteamMatchmaking.GetNumLobbyMembers(lobby) : 0);

        /// Everyone in the room, the owner first.
        public IReadOnlyList<(string Name, bool Owner, CSteamID Id)> Members()
        {
            var members = new List<(string, bool, CSteamID)>();
            if (InRoom)
            {
                var owner = SteamMatchmaking.GetLobbyOwner(lobby);
                for (var i = 0; i < SteamMatchmaking.GetNumLobbyMembers(lobby); i++)
                {
                    var id = SteamMatchmaking.GetLobbyMemberByIndex(lobby, i);
                    members.Add((SteamFriends.GetFriendPersonaName(id), id == owner, id));
                }
            }
            return members.OrderByDescending(m => m.Item2).ToList();
        }

        public void Invite()
        {
            if (InRoom)
            {
                SteamFriends.ActivateGameOverlayInviteDialog(lobby);
            }
        }

        public void ToggleLocked()
        {
            if (IsOwner)
            {
                locked = !locked;
                SteamMatchmaking.SetLobbyJoinable(lobby, !locked);
            }
        }

        public void TogglePublic()
        {
            visibility = IsPublic ? ELobbyType.k_ELobbyTypeFriendsOnly : ELobbyType.k_ELobbyTypePublic;
            if (IsOwner)
            {
                SteamMatchmaking.SetLobbyType(lobby, visibility);
            }
        }

        public void KickPartner()
        {
            if (!IsOwner)
            {
                return;
            }
            var self = SteamUser.GetSteamID();
            foreach (var (_, _, id) in Members().Where(m => m.Id != self))
            {
                kicked.Add(id.m_SteamID);
                log.Information("RINGO removing a player from the room");
            }
            net.Kick();
        }

        /// The owner leaving closes the room for everyone.
        public void Leave()
        {
            if (InRoom)
            {
                net.Leave();
                Close();
            }
        }

        void OnKicked()
        {
            Close();
            Message = ("你已被房主移出房间", "The host removed you from the room");
        }

        void Close()
        {
            SteamMatchmaking.LeaveLobby(lobby);
            SteamFriends.SetRichPresence(ConnectKey, null);
            lobby = CSteamID.Nil;
            locked = false;
            kicked.Clear();
            toldRun = "";
            toldLevel = "";
        }

        bool MayConnect(CSteamID player)
        {
            if (kicked.Contains(player.m_SteamID))
            {
                return false;
            }
            return Members().Any(m => m.Id == player);
        }
    }
}
