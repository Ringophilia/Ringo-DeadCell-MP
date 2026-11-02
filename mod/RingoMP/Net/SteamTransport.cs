using Serilog;
using Steamworks;

namespace RingoMP.Net
{
    /// Steam's networking sockets carry both kinds of packet over one connection,
    /// through its relays when the players cannot reach each other directly. They
    /// also speak plain IP, which is how two games on one Steam account test all of
    /// this: an account cannot open a P2P connection to itself.
    /// The joining side keeps dialling until the link is back.
    public sealed unsafe class SteamTransport : ITransport
    {
        const int MaxMessagesPerUpdate = 64;
        const int VirtualPort = 0;
        const int CloseReason = 0;
        const double RetrySeconds = 3;

        readonly ILogger log;
        readonly Queue<byte[]> inbox = new();
        readonly IntPtr[] messages = new IntPtr[MaxMessagesPerUpdate];
        readonly Callback<SteamNetConnectionStatusChangedCallback_t> statusChanged;
        readonly Func<HSteamNetConnection>? dial;
        readonly Func<CSteamID, bool>? mayConnect;
        readonly HSteamListenSocket listener = HSteamListenSocket.Invalid;
        HSteamNetConnection connection = HSteamNetConnection.Invalid;
        double retryIn;
        bool dialling = true;

        public bool Connected { get; private set; }

        SteamTransport(ILogger log, Func<HSteamListenSocket>? listen, Func<HSteamNetConnection>? dial, Func<CSteamID, bool>? mayConnect)
        {
            this.log = log;
            this.dial = dial;
            this.mayConnect = mayConnect;
            statusChanged = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnStatusChanged);
            if (listen != null)
            {
                listener = listen();
                log.Information("RINGO listening: {ok}", listener != HSteamListenSocket.Invalid);
            }
        }

        public static SteamTransport HostP2P(ILogger log, Func<CSteamID, bool> mayConnect)
        {
            SteamNetworkingUtils.InitRelayNetworkAccess();
            return new SteamTransport(log, () => SteamNetworkingSockets.CreateListenSocketP2P(VirtualPort, 0, null), null, mayConnect);
        }

        public static SteamTransport JoinP2P(ILogger log, CSteamID host)
        {
            SteamNetworkingUtils.InitRelayNetworkAccess();
            var identity = new SteamNetworkingIdentity();
            identity.SetSteamID(host);
            return new SteamTransport(log, null, () => SteamNetworkingSockets.ConnectP2P(ref identity, VirtualPort, 0, null), null);
        }

        public static SteamTransport HostIp(ILogger log, ushort port, float lossPercent, int lagMs)
        {
            var address = Address("0.0.0.0", port);
            var options = TestOptions(lossPercent, lagMs);
            return new SteamTransport(log, () => SteamNetworkingSockets.CreateListenSocketIP(ref address, options.Length, options), null, null);
        }

        public static SteamTransport JoinIp(ILogger log, string host, ushort port, float lossPercent, int lagMs)
        {
            var address = Address(host, port);
            var options = TestOptions(lossPercent, lagMs);
            return new SteamTransport(log, null, () => SteamNetworkingSockets.ConnectByIPAddress(ref address, options.Length, options), null);
        }

        static SteamNetworkingIPAddr Address(string host, ushort port)
        {
            var address = new SteamNetworkingIPAddr();
            address.Clear();
            address.ParseString($"{host}:{port}");
            return address;
        }

        /// Both games on one account have the same identity, so IP links skip
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

        void OnStatusChanged(SteamNetConnectionStatusChangedCallback_t change)
        {
            switch (change.m_info.m_eState)
            {
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                    Accept(change);
                    break;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                    log.Information("RINGO link up");
                    Connected = true;
                    break;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                    log.Information("RINGO link closed: {reason}", change.m_info.m_szEndDebug);
                    SteamNetworkingSockets.CloseConnection(change.m_hConn, CloseReason, null, false);
                    if (change.m_hConn == connection)
                    {
                        Lose();
                    }
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
            if (mayConnect != null && !mayConnect(change.m_info.m_identityRemote.GetSteamID()))
            {
                SteamNetworkingSockets.CloseConnection(change.m_hConn, CloseReason, null, false);
                return;
            }
            // A partner coming back may get here before the old link has timed out.
            if (connection != HSteamNetConnection.Invalid)
            {
                SteamNetworkingSockets.CloseConnection(connection, CloseReason, null, false);
            }
            connection = change.m_hConn;
            SteamNetworkingSockets.AcceptConnection(change.m_hConn);
        }

        void Lose()
        {
            connection = HSteamNetConnection.Invalid;
            Connected = false;
            retryIn = RetrySeconds;
        }

        public void Update(double dt)
        {
            SteamAPI.RunCallbacks();
            SteamNetworkingSockets.RunCallbacks();
            if (dial != null && dialling && connection == HSteamNetConnection.Invalid)
            {
                retryIn -= dt;
                if (retryIn <= 0)
                {
                    retryIn = RetrySeconds;
                    connection = dial();
                }
            }
            if (connection == HSteamNetConnection.Invalid)
            {
                return;
            }
            var count = SteamNetworkingSockets.ReceiveMessagesOnConnection(connection, messages, MaxMessagesPerUpdate);
            for (var i = 0; i < count; i++)
            {
                var message = SteamNetworkingMessage_t.FromIntPtr(messages[i]);
                var packet = new byte[message.m_cbSize];
                System.Runtime.InteropServices.Marshal.Copy(message.m_pData, packet, 0, packet.Length);
                inbox.Enqueue(packet);
                SteamNetworkingMessage_t.Release(messages[i]);
            }
        }

        public void SendReliable(byte[] packet) => Send(packet, Constants.k_nSteamNetworkingSend_ReliableNoNagle);

        public void SendUnreliable(byte[] packet) => Send(packet, Constants.k_nSteamNetworkingSend_UnreliableNoDelay);

        void Send(byte[] packet, int flags)
        {
            if (!Connected)
            {
                return;
            }
            fixed (byte* data = packet)
            {
                SteamNetworkingSockets.SendMessageToConnection(connection, (IntPtr)data, (uint)packet.Length, flags, out _);
            }
        }

        public bool TryReceive(out byte[] packet) => inbox.TryDequeue(out packet!);

        /// For tests: cuts the link the way a network failure would, for good if asked to.
        public void Cut(bool forGood)
        {
            SteamNetworkingSockets.CloseConnection(connection, CloseReason, null, false);
            Lose();
            dialling = !forGood;
        }

        public void Dispose()
        {
            statusChanged.Dispose();
            if (connection != HSteamNetConnection.Invalid)
            {
                SteamNetworkingSockets.CloseConnection(connection, CloseReason, null, false);
            }
            if (listener != HSteamListenSocket.Invalid)
            {
                SteamNetworkingSockets.CloseListenSocket(listener);
            }
        }
    }
}
