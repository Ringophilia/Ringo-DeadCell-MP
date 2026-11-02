using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace RingoMP.Net
{
    /// TCP for reliable packets, UDP for the rest, on the same port number.
    /// Sockets are read on background threads, which never touch game objects.
    public sealed class LanTransport : ITransport
    {
        const int LengthPrefixBytes = 4;
        const int MaxPacketBytes = 1 << 20;

        readonly ConcurrentQueue<byte[]> inbox = new();
        readonly UdpClient udp;
        readonly CancellationTokenSource stop = new();
        readonly object sendLock = new();
        TcpClient? tcp;
        NetworkStream? stream;
        IPEndPoint? peer;

        public bool Connected => stream != null && tcp is { Connected: true };

        LanTransport(UdpClient udp)
        {
            this.udp = udp;
            udp.Client.ReceiveBufferSize = 1 << 20;
        }

        public static LanTransport Host(int port)
        {
            var transport = new LanTransport(new UdpClient(new IPEndPoint(IPAddress.Any, port)));
            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            transport.Run(async () =>
            {
                var client = await listener.AcceptTcpClientAsync(transport.stop.Token);
                listener.Stop();
                transport.Attach(client);
            });
            return transport;
        }

        public static LanTransport Join(string address, int port, int localUdpPort)
        {
            var transport = new LanTransport(new UdpClient(new IPEndPoint(IPAddress.Any, localUdpPort)));
            transport.peer = new IPEndPoint(IPAddress.Parse(address), port);
            transport.Run(async () =>
            {
                var client = new TcpClient();
                await client.ConnectAsync(transport.peer.Address, port, transport.stop.Token);
                transport.Attach(client);
            });
            return transport;
        }

        void Attach(TcpClient client)
        {
            client.NoDelay = true;
            tcp = client;
            stream = client.GetStream();
            peer ??= new IPEndPoint(((IPEndPoint)client.Client.RemoteEndPoint!).Address, 0);
            Run(ReadReliable);
            Run(ReadUnreliable);
        }

        void Run(Func<Task> loop) => _ = Task.Run(async () =>
        {
            try
            {
                await loop();
            }
            catch (Exception) when (stop.IsCancellationRequested || !Connected)
            {
                // The link went away; Connected reports it to the game thread.
            }
        });

        async Task ReadReliable()
        {
            var header = new byte[LengthPrefixBytes];
            while (!stop.IsCancellationRequested)
            {
                await stream!.ReadExactlyAsync(header, stop.Token);
                var length = BitConverter.ToInt32(header);
                if (length is <= 0 or > MaxPacketBytes)
                {
                    throw new IOException($"bad packet length {length}");
                }
                var body = new byte[length];
                await stream.ReadExactlyAsync(body, stop.Token);
                inbox.Enqueue(body);
            }
        }

        async Task ReadUnreliable()
        {
            while (!stop.IsCancellationRequested)
            {
                var result = await udp.ReceiveAsync(stop.Token);
                // The host learns the client's UDP address from its first datagram.
                peer = result.RemoteEndPoint;
                inbox.Enqueue(result.Buffer);
            }
        }

        public void Update(double dt)
        {
        }

        public void SendReliable(byte[] packet)
        {
            lock (sendLock)
            {
                try
                {
                    stream?.Write(BitConverter.GetBytes(packet.Length));
                    stream?.Write(packet);
                }
                catch (IOException)
                {
                    // The link went away; Connected reports it to the game thread.
                }
            }
        }

        public void SendUnreliable(byte[] packet)
        {
            if (peer is { Port: > 0 } target)
            {
                udp.Send(packet, packet.Length, target);
            }
        }

        public bool TryReceive(out byte[] packet) => inbox.TryDequeue(out packet!);

        public void Dispose()
        {
            stop.Cancel();
            udp.Dispose();
            tcp?.Dispose();
        }
    }
}
