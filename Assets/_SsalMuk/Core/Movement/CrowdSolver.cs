using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    // Soft steering only: overlaps never cause position correction or move another unit.
    public sealed class CrowdSolver
    {
        private readonly MovementSettings settings;
        private readonly Dictionary<GridCell, CrowdCell> cells = new Dictionary<GridCell, CrowdCell>();
        private readonly List<CrowdCell> cellPool = new List<CrowdCell>();
        private int usedCellCount;
        public CrowdSolver(MovementSettings settings = null) { this.settings = settings ?? new MovementSettings(); }

        internal void BeginStep(IReadOnlyList<UnitModel> units)
        {
            cells.Clear(); usedCellCount = 0;
            foreach (var unit in units)
            {
                if (!unit.IsAlive || unit.Kind == UnitKind.Player || unit.Kind == UnitKind.Air) continue;
                var key = GridCell.At(unit.Position);
                if (!cells.TryGetValue(key, out var cell))
                {
                    if (usedCellCount == cellPool.Count) cellPool.Add(new CrowdCell());
                    cell = cellPool[usedCellCount++]; cell.Reset(key); cells.Add(key, cell);
                }
                cell.Add(unit);
            }
        }

        internal DVec2 Steer(WorldStore world, UnitModel unit, DVec2 movement, double maximumRadius)
        {
            double length = movement.Length;
            if (unit.Kind == UnitKind.Player || unit.Kind == UnitKind.Air || length < 1e-9 || settings.CrowdAvoidanceStrength == 0) return movement;
            var avoidance = DVec2.Zero; double weightSum = 0;
            var origin = GridCell.At(unit.Position);
            int range = Math.Min(3, (int)Math.Ceiling(unit.BodyRadius + maximumRadius) + 1);
            for (int y = -range; y <= range; y++) for (int x = -range; x <= range; x++)
            {
                var key = Offset(origin, x, y);
                if (!cells.TryGetValue(key, out var cell)) continue;
                int count = cell.Count; var offsets = cell.OffsetSum;
                if (key.Equals(origin))
                {
                    count--;
                    offsets -= key.Center.DisplacementTo(unit.Position);
                }
                if (count <= 0) continue;
                var otherPosition = key.Center.Offset(offsets / count);
                var away = otherPosition.DisplacementTo(unit.Position);
                double distance = away.Length, separation = unit.BodyRadius + cell.MaximumRadius;
                if (distance >= separation) continue;
                double weight = (1 - distance / separation) * count;
                long representative = cell.RepresentativeExcluding(unit.Id);
                avoidance += (distance > 1e-9 ? away / distance : StableNormal(unit.Id, representative)) * weight;
                weightSum += weight;
            }
            if (weightSum == 0) return movement;
            avoidance /= Math.Max(1, weightSum);
            var result = movement + avoidance * (length * settings.CrowdAvoidanceStrength);
            // Never add speed, teleport, or redirect attack knockback. Terrain remains authoritative.
            return result.Length > length ? result.Normalized * length : result;
        }

        private static DVec2 StableNormal(long a, long b)
        {
            ulong bits = StableHash.Combine(71, Math.Min(a, b), Math.Max(a, b), 1);
            double angle = (bits & 65535) * (Math.PI * 2 / 65536);
            var normal = new DVec2(Math.Cos(angle), Math.Sin(angle)); return a < b ? normal : -normal;
        }

        private static GridCell Offset(GridCell origin, int x, int y)
        {
            long chunkX = origin.Chunk.X, chunkY = origin.Chunk.Y;
            int localX = origin.X + x, localY = origin.Y + y;
            if (localX < 0) { chunkX = checked(chunkX - 1); localX += ChunkData.Size; }
            else if (localX >= ChunkData.Size) { chunkX = checked(chunkX + 1); localX -= ChunkData.Size; }
            if (localY < 0) { chunkY = checked(chunkY - 1); localY += ChunkData.Size; }
            else if (localY >= ChunkData.Size) { chunkY = checked(chunkY + 1); localY -= ChunkData.Size; }
            return new GridCell(new ChunkCoord(chunkX, chunkY), localX, localY);
        }

        private sealed class CrowdCell
        {
            private long firstId = long.MaxValue, secondId = long.MaxValue;
            public GridCell Cell { get; private set; }
            public int Count { get; private set; }
            public DVec2 OffsetSum { get; private set; }
            public double MaximumRadius { get; private set; }
            public void Reset(GridCell cell)
            {
                Cell = cell; Count = 0; OffsetSum = DVec2.Zero; MaximumRadius = 0;
                firstId = secondId = long.MaxValue;
            }
            public void Add(UnitModel unit)
            {
                Count++; OffsetSum += Cell.Center.DisplacementTo(unit.Position);
                MaximumRadius = Math.Max(MaximumRadius, unit.BodyRadius);
                if (unit.Id < firstId) { secondId = firstId; firstId = unit.Id; }
                else if (unit.Id < secondId) secondId = unit.Id;
            }
            public long RepresentativeExcluding(long id) => firstId != id ? firstId : secondId;
        }
    }
}
