namespace RingoMP.Net
{
    /// Links to the other players. A host has one per player who joined; a player
    /// who joined has one, to the host, which is link 0.
    public interface ITransport : IDisposable
    {
        /// At least one link is up.
        bool Connected { get; }

        /// The links that are up now.
        IReadOnlyCollection<int> Links { get; }

        /// Runs every frame on the game thread, before anything is received.
        void Update(double dt);

        /// Reliable packets arrive once and in order while the link is up, and are
        /// lost when it is down. The others may be lost or reordered; they carry
        /// state that the next one replaces.
        void Send(int link, byte[] packet, bool reliable);

        /// Called on the game thread. Returns false when nothing is waiting.
        bool TryReceive(out byte[] packet, out int link);
    }

    public static class TransportSending
    {
        public static void SendReliable(this ITransport transport, byte[] packet) => SendAll(transport, packet, true, -1);

        public static void SendUnreliable(this ITransport transport, byte[] packet) => SendAll(transport, packet, false, -1);

        public static void SendAll(this ITransport transport, byte[] packet, bool reliable, int except)
        {
            foreach (var link in transport.Links)
            {
                if (link != except)
                {
                    transport.Send(link, packet, reliable);
                }
            }
        }
    }
}
