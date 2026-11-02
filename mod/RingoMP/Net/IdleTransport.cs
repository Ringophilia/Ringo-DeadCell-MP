namespace RingoMP.Net
{
    /// The link of a player who is alone. A session exists from the start of the
    /// game, so that a friend can join whatever the player is doing by then.
    public sealed class IdleTransport : ITransport
    {
        public bool Connected => false;

        public void Update(double dt)
        {
        }

        public void SendReliable(byte[] packet)
        {
        }

        public void SendUnreliable(byte[] packet)
        {
        }

        public bool TryReceive(out byte[] packet)
        {
            packet = [];
            return false;
        }

        public void Dispose()
        {
        }
    }
}
