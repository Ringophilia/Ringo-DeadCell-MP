using Serilog;
using Steamworks;

namespace RingoMP.Net
{
    /// Steam's networking sockets carry both kinds of packet over one connection,
    /// through its relays when the players cannot reach each other directly. They
    /// also speak plain IP, which is how games on one Steam account test all of
    /// this: an account cannot open a P2P connection to itself.
    /// A host takes a connection from each player who joins, up to the room's size;
    /// the joining side keeps dialling until its link is back.
    public sealed unsafe class SteamTransport : ITransport
    {
        public const int HostLink = 0;
        const int MaxMessagesPerUpdate = 64;
        const int VirtualPort = 0;
        const int CloseReason = 0;
        const double RetrySeconds = 3;

        readonly ILogger log;
        readonly Queue<(byte[] Packet, int Link)> inbox = new();
        readonly IntPtr[] messages = new IntPtr[MaxMessagesPerUpdate];
        readonly Callback<SteamNetConnectionStatusChangedCallback_t> statusChanged;
        readonly Func<HSteamNetConnection>? dial;
        readonly Func<CSteamID, bool>? mayConnect;
        readonly int maxLinks;
        readonly HSteamListenSocket listener = HSteamListenSocket.Invalid;
        // A link is known from the moment it is accepted or dialled, and up once connected.
        readonly Dictionary<HSteamNetConnection, int> links = [];
        readonly HashSet<int> up = [];
        double retryIn;
        bool dialling = true;

        public bool Connected => up.Count > 0;

        public IReadOnlyCollection<int> Links => up;

        SteamTransport(ILogger log, Func<HSteamListenSocket>? listen, Func<HSteamNetConnection>? dial, Func<CSteamID, bool>? mayConnect, int maxLinks)
        {
            this.log = log;
            this.dial = dial;
            this.mayConnect = mayConnect;
            this.maxLinks = maxLinks;
            statusChanged = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnStatusChanged);
            if (listen != null)
            {
                listener = listen();
                log.Information("RINGO listening: {ok}", listener != HSteamListenSocket.Invalid);
            }
        }

        public static SteamTransport HostP2P(ILogger log, Func<CSteamID, bool> mayConnect, int maxPlayers)
        {
            SteamNetworkingUtils.InitRelayNetworkAccess();
            return new SteamTransport(log, () => SteamNetworkingSockets.CreateListenSocketP2P(VirtualPort, 0, null), null, mayConnect, maxPlayers - 1);
        }

        public static SteamTransport JoinP2P(ILogger log, CSteamID host)
        {
            SteamNetworkingUtils.InitRelayNetworkAccess();
            var identity = new SteamNetworkingIdentity();
            identity.SetSteamID(host);
            return new SteamTransport(log, null, () => SteamNetworkingSockets.ConnectP2P(ref identity, VirtualPort, 0, null), null, 1);
        }

        public static SteamTransport HostIp(ILogger log, ushort port, float lossPercent, int lagMs, int maxPlayers)
        {
            var address = Address("0.0.0.0", port);
            var options = TestOptions(lossPercent, lagMs);
            return new SteamTransport(log, () => SteamNetworkingSockets.CreateListenSocketIP(ref address, options.Length, options), null, null, maxPlayers - 1);
        }

        public static SteamTransport JoinIp(ILogger log, string host, ushort port, float lossPercent, int lagMs)
        {
            var address = Address(host, port);
            var options = TestOptions(lossPercent, lagMs);
            return new SteamTransport(log, null, () => SteamNetworkingSockets.ConnectByIPAddress(ref address, options.Length, options), null, 1);
        }

        static SteamNetworkingIPAddr Address(string host, ushort port)
        {
            var address = new SteamNetworkingIPAddr();
            address.Clear();
            address.ParseString($"{host}:{port}");
            return address;
        }

        /// Games on one account have the same identity, so IP links skip
        /// authentication. Simulated loss and lag only exist as global settings.
        static SteamNetworkingConfigValue_t[] TestOptions(float lossPercent, int lagMs)
        {
            SetGlobal(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_FakePacketLoss_Send,
                ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Float, BitConverter.SingleToInt32Bits(lossPercent));
            SetGlobal(ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_FakePacketLag_Send,
                ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32, lagMs);
            return
            [
                new()
                {
                    m_eValue = ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_IP_AllowWithoutAuth,
                    m_eDataType = ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
                    m_val = new SteamNetworkingConfigValue_t.OptionValue { m_int32 = 1 },
                },
            ];
        }

        static void SetGlobal(ESteamNetworkingConfigValue setting, ESteamNetworkingConfigDataType type, int bits)
        {
            SteamNetworkingUtils.SetConfigValue(setting, ESteamNetworkingConfigScope.k_ESteamNetworkingConfig_Global,
                IntPtr.Zero, type, (IntPtr)(&bits));
        }

        bool IsHost => listener != HSteamListenSocket.Invalid;

        void OnStatusChanged(SteamNetConnectionStatusChangedCallback_t change)
        {
            switch (change.m_info.m_eState)
            {
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                    Accept(change);
                    break;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                    if (links.TryGetValue(change.m_hConn, out var link))
                    {
                        log.Information("RINGO link {l} up", link);
                        up.Add(link);
                    }
                    break;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                    log.Information("RINGO link closed: {reason}", change.m_info.m_szEndDebug);
                    // What arrived before the close still counts: a goodbye, for one.
                    if (links.TryGetValue(change.m_hConn, out var closing))
                    {
                        Drain(change.m_hConn, closing);
                    }
                    SteamNetworkingSockets.CloseConnection(change.m_hConn, CloseReason, null, false);
                    Lose(change.m_hConn);
                    break;
            }
        }

        void Accept(SteamNetConnectionStatusChangedCallback_t change)
        {
            // A dialled connection reports this state too, without a listen socket.
            if (change.m_info.m_hListenSocket == HSteamListenSocket.Invalid)
            {
                return;
            }
            if ((mayConnect != null && !mayConnect(change.m_info.m_identityRemote.GetSteamID())) || links.Count >= maxLinks)
            {
                log.Information("RINGO turned a connection away, {n} of {m} links taken", links.Count, maxLinks);
                SteamNetworkingSockets.CloseConnection(change.m_hConn, CloseReason, null, false);
                return;
            }
            // Players are numbered from 1, the host being 0; a free number is reused.
            var link = 1;
            while (links.ContainsValue(link))
            {
                link++;
            }
            links[change.m_hConn] = link;
            SteamNetworkingSockets.AcceptConnection(change.m_hConn);
        }

        void Lose(HSteamNetConnection connection)
        {
            if (links.Remove(connection, out var link))
            {
                up.Remove(link);
                retryIn = RetrySeconds;
            }
        }

        public void Update(double dt)
        {
            SteamAPI.RunCallbacks();
            SteamNetworkingSockets.RunCallbacks();
            if (dial != null && dialling && links.Count == 0)
            {
                retryIn -= dt;
                if (retryIn <= 0)
                {
                    retryIn = RetrySeconds;
                    var connection = dial();
                    if (connection != HSteamNetConnection.Invalid)
                    {
                        links[connection] = HostLink;
                    }
                }
            }
            foreach (var (connection, link) in links.ToList())
            {
                Receive(connection, link);
            }
        }

        int Receive(HSteamNetConnection connection, int link)
        {
            var count = SteamNetworkingSockets.ReceiveMessagesOnConnection(connection, messages, MaxMessagesPerUpdate);
            for (var i = 0; i < count; i++)
            {
                var message = SteamNetworkingMessage_t.FromIntPtr(messages[i]);
                var packet = new byte[message.m_cbSize];
                System.Runtime.InteropServices.Marshal.Copy(message.m_pData, packet, 0, packet.Length);
                inbox.Enqueue((packet, link));
                SteamNetworkingMessage_t.Release(messages[i]);
            }
            return count;
        }

        void Drain(HSteamNetConnection connection, int link)
        {
            while (Receive(connection, link) > 0)
            {
            }
        }

        public void Send(int link, byte[] packet, bool reliable)
        {
            if (!up.Contains(link))
            {
                return;
            }
            var flags = reliable ? Constants.k_nSteamNetworkingSend_ReliableNoNagle : Constants.k_nSteamNetworkingSend_UnreliableNoDelay;
            foreach (var (connection, other) in links)
            {
                if (other == link)
                {
                    fixed (byte* data = packet)
                    {
                        SteamNetworkingSockets.SendMessageToConnection(connection, (IntPtr)data, (uint)packet.Length, flags, out _);
                    }
                    return;
                }
            }
        }

        public bool TryReceive(out byte[] packet, out int link)
        {
            if (inbox.TryDequeue(out var next))
            {
                (packet, link) = next;
                return true;
            }
            packet = [];
            link = 0;
            return false;
        }

        /// For tests: cuts every link the way a network failure would. A player who
        /// joined stops dialling too when it is for good.
        public void Cut(bool forGood)
        {
            foreach (var connection in links.Keys.ToList())
            {
                SteamNetworkingSockets.CloseConnection(connection, CloseReason, null, false);
                Lose(connection);
            }
            dialling = !forGood;
        }

        public void Dispose()
        {
            statusChanged.Dispose();
            foreach (var connection in links.Keys)
            {
                SteamNetworkingSockets.CloseConnection(connection, CloseReason, null, false);
            }
            if (IsHost)
            {
                SteamNetworkingSockets.CloseListenSocket(listener);
            }
        }
    }
}
