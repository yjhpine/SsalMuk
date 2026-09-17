using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    public enum ChunkSide { West, East, South, North }

    public sealed class ChunkData
    {
        public const int Size = 32;
        private readonly bool[] blocked;
        private readonly int[] openings;
        private readonly uint[] blockedRows = new uint[Size];
        public bool HasObstacles { get; }
        public IReadOnlyList<FurniturePlacement> Furniture { get; }
        public ChunkCoord Coord { get; }
        public int Version { get; }
        public string Fingerprint { get; }
        public bool UsedSafeLayout { get; }
        public ChunkData(ChunkCoord coord, bool[] blocked, int version, int[] openings = null, bool usedSafeLayout = false,
            IReadOnlyList<FurniturePlacement> furniture = null)
        {
            if (blocked == null || blocked.Length != Size * Size) throw new ArgumentException("A chunk needs 32 by 32 cells.", nameof(blocked));
            if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
            if (openings != null && openings.Length != 4) throw new ArgumentException("Four boundary openings are required.", nameof(openings));
            Coord = coord; Version = version; this.blocked = (bool[])blocked.Clone();
            this.openings = openings == null ? new[] { 14, 14, 14, 14 } : (int[])openings.Clone();
            UsedSafeLayout = usedSafeLayout;
            var placements = new FurniturePlacement[furniture?.Count ?? 0];
            for (int i = 0; i < placements.Length; i++)
            {
                var item = furniture[i];
                if (item.Width < 1 || item.Height < 1) throw new ArgumentException("Invalid furniture footprint.", nameof(furniture));
                for (int y = item.Y; y < item.Y + item.Height; y++) for (int x = item.X; x < item.X + item.Width; x++)
                    if (!IsBlocked(x, y)) throw new ArgumentException("Furniture must occupy blocked cells.", nameof(furniture));
                placements[i] = item;
            }
            Furniture = Array.AsReadOnly(placements);
            ulong hash = StableHash.Append(StableHash.Offset, (uint)version);
            foreach (bool cell in blocked) hash = StableHash.Append(hash, cell ? 1UL : 0UL);
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
                if (blocked[y * Size + x]) { blockedRows[y] |= 1U << x; HasObstacles = true; }
            foreach (var item in placements)
            {
                hash = StableHash.Append(hash, (ulong)item.Kind);
                hash = StableHash.Append(hash, (ulong)(item.X | item.Y << 5 | item.Width << 10 | item.Height << 16 | item.Variation << 22));
            }
            Fingerprint = hash.ToString("x16");
        }
        public bool IsBlocked(int x, int y)
        {
            if (x < 0 || x >= Size || y < 0 || y >= Size) throw new ArgumentOutOfRangeException(nameof(x));
            return blocked[y * Size + x];
        }
        public int OpeningStart(ChunkSide side) => openings[(int)side];
        internal uint BlockedRow(int y) => blockedRows[y];
    }
}
