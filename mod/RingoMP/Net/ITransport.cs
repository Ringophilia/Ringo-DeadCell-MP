namespace RingoMP.Net
{
    /// A two-way link to the other player, over Steam or the local network.
    public interface ITransport : IDisposable
    {
        bool Connected { get; }

        /// Runs every frame on the game thread, before anything is received.
        void Update(double dt);

        /// Arrives once and in order while the link is up; lost when it is down.
        void SendReliable(byte[] packet);

        /// May be lost or reordered; for state that is replaced by the next one.
        void SendUnreliable(byte[] packet);

        /// Called on the game thread. Returns false when nothing is waiting.
        bool TryReceive(out byte[] packet);
    }
}
