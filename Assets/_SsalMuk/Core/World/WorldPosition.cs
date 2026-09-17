using System;

namespace SsalMuk.Core
{
    public readonly struct WorldPosition : IEquatable<WorldPosition>
    {
        public const double ChunkSize = 32;
        public ChunkCoord Chunk { get; }
        public DVec2 Local { get; }

        public WorldPosition(ChunkCoord chunk, DVec2 local)
        {
            Normalize(chunk.X, local.X, out long x, out double localX);
            Normalize(chunk.Y, local.Y, out long y, out double localY);
            Chunk = new ChunkCoord(x, y);
            Local = new DVec2(localX, localY);
        }

        private static void Normalize(long chunk, double local, out long normalizedChunk, out double normalizedLocal)
        {
            double steps = Math.Floor(local / ChunkSize);
            if (steps < long.MinValue || steps >= 9223372036854775808d)
                throw new OverflowException("Coordinate exceeds the supported integer representation.");
            normalizedChunk = checked(chunk + (long)steps);
            normalizedLocal = local - steps * ChunkSize;
            // Floating point rounding at a boundary must not leave a local value outside [0, 32).
            if (normalizedLocal >= ChunkSize) { normalizedChunk = checked(normalizedChunk + 1); normalizedLocal -= ChunkSize; }
            if (normalizedLocal < 0) { normalizedChunk = checked(normalizedChunk - 1); normalizedLocal += ChunkSize; }
        }

        public static WorldPosition FromLocal(DVec2 local) => new WorldPosition(default, local);
        public WorldPosition Offset(DVec2 displacement) => new WorldPosition(Chunk, Local + displacement);

        public DVec2 DisplacementTo(WorldPosition other)
        {
            if (Chunk.Equals(other.Chunk)) return other.Local - Local;
            // Subtract integer chunks before converting to double, retaining small local differences far away.
            double x = (double)((decimal)other.Chunk.X - Chunk.X) * ChunkSize + other.Local.X - Local.X;
            double y = (double)((decimal)other.Chunk.Y - Chunk.Y) * ChunkSize + other.Local.Y - Local.Y;
            return new DVec2(x, y);
        }

        public double DistanceTo(WorldPosition other) => DisplacementTo(other).Length;
        public bool Equals(WorldPosition other) => Chunk.Equals(other.Chunk) && Local.Equals(other.Local);
        public override bool Equals(object obj) => obj is WorldPosition other && Equals(other);
        public override int GetHashCode() => unchecked(Chunk.GetHashCode() * 397 ^ Local.GetHashCode());
        public static bool operator ==(WorldPosition a, WorldPosition b) => a.Equals(b);
        public static bool operator !=(WorldPosition a, WorldPosition b) => !a.Equals(b);
    }
}
