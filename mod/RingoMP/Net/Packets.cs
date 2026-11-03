namespace RingoMP.Net
{
    public enum PacketType : byte
    {
        Hello,
        LevelReady,
        HeroState,
        MobStates,
        MobDied,
        MobAnim,
        HitHero,
        ExitWait,
        ExitGo,
        ExitAccept,
        Rescue,
        TeamWipe,
        ItemTaken,
        ItemPlaced,
        Progress,
        Join,
        LoadoutSaved,
        LoadoutBack,
        Bye,
        Kicked,
        Look,
        LevelContents,
        // The host tells a player who joined which player they are.
        Welcome,
        // The host tells the others that a player is gone.
        PeerGone,
        // What one player sent, passed on by the host to the others: their number, then the packet.
        Relay,
    }

    /// One hero's state, sent 30 times a second by the player who owns it.
    public struct HeroStateData
    {
        public ulong Held;
        public DownState Down;
        public int Cx, Cy, Dir, Life;
        public float Xr, Yr, Dx, Dy;

        public void Write(BinaryWriter w)
        {
            w.Write(Held);
            w.Write((byte)Down);
            w.Write(Cx);
            w.Write(Cy);
            w.Write((sbyte)Dir);
            w.Write(Life);
            w.Write(Xr);
            w.Write(Yr);
            w.Write(Dx);
            w.Write(Dy);
        }

        public static HeroStateData Read(BinaryReader r) => new()
        {
            Held = r.ReadUInt64(),
            Down = (DownState)r.ReadByte(),
            Cx = r.ReadInt32(),
            Cy = r.ReadInt32(),
            Dir = r.ReadSByte(),
            Life = r.ReadInt32(),
            Xr = r.ReadSingle(),
            Yr = r.ReadSingle(),
            Dx = r.ReadSingle(),
            Dy = r.ReadSingle(),
        };
    }

    /// One mob's state, sent by the host.
    public struct MobStateData
    {
        public ushort Index;
        public int Cx, Cy, Dir, Life;
        public float Xr, Yr, Dx, Dy;

        public void Write(BinaryWriter w)
        {
            w.Write(Index);
            w.Write(Cx);
            w.Write(Cy);
            w.Write((sbyte)Dir);
            w.Write(Life);
            w.Write(Xr);
            w.Write(Yr);
            w.Write(Dx);
            w.Write(Dy);
        }

        public static MobStateData Read(BinaryReader r) => new()
        {
            Index = r.ReadUInt16(),
            Cx = r.ReadInt32(),
            Cy = r.ReadInt32(),
            Dir = r.ReadSByte(),
            Life = r.ReadInt32(),
            Xr = r.ReadSingle(),
            Yr = r.ReadSingle(),
            Dx = r.ReadSingle(),
            Dy = r.ReadSingle(),
        };
    }

    public static class Packet
    {
        public static byte[] Build(PacketType type, Action<BinaryWriter> body)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write((byte)type);
            body(writer);
            writer.Flush();
            return stream.ToArray();
        }
    }
}
