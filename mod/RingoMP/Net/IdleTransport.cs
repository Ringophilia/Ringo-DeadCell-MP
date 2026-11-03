namespace RingoMP.Net
{
    /// The link of a player who is alone. A session exists from the start of the
    /// game, so that a friend can join whatever the player is doing by then.
    public sealed class IdleTransport : ITransport
    {
        public bool Connected => false;

        public IReadOnlyCollection<int> Links => [];

        public void Update(double dt)
        {
        }

        public void Send(int link, byte[] packet, bool reliable)
        {
        }

        public bool TryReceive(out byte[] packet, out int link)
        {
            packet = [];
            link = 0;
            return false;
        }

        public void Dispose()
        {
        }
    }
}
