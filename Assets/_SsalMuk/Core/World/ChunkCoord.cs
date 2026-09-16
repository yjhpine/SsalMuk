using System;

namespace SsalMuk.Core
{
    public readonly struct ChunkCoord : IEquatable<ChunkCoord>
    {
        public long X { get; }
        public long Y { get; }
        public ChunkCoord(long x, long y) { X = x; Y = y; }
        public bool Equals(ChunkCoord other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is ChunkCoord other && Equals(other);
        public override int GetHashCode() => unchecked(X.GetHashCode() * 397 ^ Y.GetHashCode());
        public static bool operator ==(ChunkCoord a, ChunkCoord b) => a.Equals(b);
        public static bool operator !=(ChunkCoord a, ChunkCoord b) => !a.Equals(b);
    }
}
