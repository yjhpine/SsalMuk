using System;

namespace SsalMuk.Core
{
    public enum ChunkSide { West, East, South, North }

    public sealed class ChunkData
    {
        public const int Size = 32;
        private readonly bool[] blocked;
        private readonly int[] openings;
        public ChunkCoord Coord { get; }
        public int Version { get; }
        public string Fingerprint { get; }
        public bool UsedSafeLayout { get; }
        public ChunkData(ChunkCoord coord, bool[] blocked, int version, int[] openings = null, bool usedSafeLayout = false)
        {
            if (blocked == null || blocked.Length != Size * Size) throw new ArgumentException("A chunk needs 32 by 32 cells.", nameof(blocked));
            if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
            if (openings != null && openings.Length != 4) throw new ArgumentException("Four boundary openings are required.", nameof(openings));
            Coord = coord; Version = version; this.blocked = (bool[])blocked.Clone();
            this.openings = openings == null ? new[] { 14, 14, 14, 14 } : (int[])openings.Clone();
            UsedSafeLayout = usedSafeLayout;
            ulong hash = StableHash.Append(StableHash.Offset, (uint)version);
            foreach (bool cell in blocked) hash = StableHash.Append(hash, cell ? 1UL : 0UL);
            Fingerprint = hash.ToString("x16");
        }
        public bool IsBlocked(int x, int y)
        {
            if (x < 0 || x >= Size || y < 0 || y >= Size) throw new ArgumentOutOfRangeException(nameof(x));
            return blocked[y * Size + x];
        }
        public int OpeningStart(ChunkSide side) => openings[(int)side];
    }
}
