using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    public sealed class WorldQuery : IWorldQuery
    {
        private readonly WorldStore world;
        private readonly SpatialIndex units, experience;
        // World simulation queries are synchronous. Reuse this scratch storage for exact static geometry.
        private readonly List<GridCell> blockedCandidates = new List<GridCell>(32);
        internal WorldQuery(WorldStore world, SpatialIndex units, SpatialIndex experience)
        { this.world = world; this.units = units; this.experience = experience; }
        public bool TryGetUnit(long id, out UnitModel unit) => world.Units.TryGet(id, out unit);
        public long? FindNearestEnemy(WorldPosition position) => units.FindNearest(position,
            id => world.Units.TryGet(id, out var unit) && unit.IsAlive && unit.Kind != UnitKind.Player);
        public IReadOnlyList<long> QueryCircle(WorldPosition position, double radius) => units.QueryCircle(position, radius);
        public void QueryCircle(WorldPosition position, double radius, List<long> results) => units.QueryCircle(position, radius, results);
        public IReadOnlyList<long> QueryExperienceCircle(WorldPosition position, double radius) => experience.QueryCircle(position, radius);
        public bool IsCircleFree(WorldPosition position, double radius)
        {
            SpatialIndex.RequireRadius(radius);
            CollectBlocked(position, DVec2.Zero, radius);
            foreach (var cell in blockedCandidates)
            {
                var min = position.DisplacementTo(new WorldPosition(cell.Chunk, new DVec2(cell.X, cell.Y)));
                if (CircleSweep.OverlapsBox(DVec2.Zero, radius, min, min + new DVec2(1, 1))) return false;
            }
            return true;
        }
        public SweepHit? SweepCircle(WorldPosition position, DVec2 displacement, double radius)
        {
            SpatialIndex.RequireRadius(radius);
            CollectBlocked(position, displacement, radius);
            SweepHit? best = null;
            foreach (var cell in blockedCandidates)
            {
                var min = position.DisplacementTo(new WorldPosition(cell.Chunk, new DVec2(cell.X, cell.Y)));
                // AgainstBox also rejects invalid starts, including zero-length sweeps.
                var hit = CircleSweep.AgainstBox(DVec2.Zero, displacement, radius, min, min + new DVec2(1, 1));
                if (hit.HasValue && (!best.HasValue || hit.Value.Fraction < best.Value.Fraction))
                    best = new SweepHit(hit.Value.Fraction, hit.Value.Normal, position.Offset(displacement * hit.Value.Fraction));
            }
            return best;
        }

        internal bool IsSweepRegionEmpty(WorldPosition position, DVec2 displacement, double radius)
        {
            SpatialIndex.RequireRadius(radius);
            CollectBlocked(position, displacement, radius);
            return blockedCandidates.Count == 0;
        }

        private void CollectBlocked(WorldPosition start, DVec2 displacement, double radius)
        {
            blockedCandidates.Clear();
            // Small local sweeps are the common movement/navigation case. Row masks skip empty
            // terrain without allocating a HashSet or visiting every neighbouring cell.
            if (Math.Abs(displacement.X) + 2 * radius <= 16 && Math.Abs(displacement.Y) + 2 * radius <= 16)
            {
                const double padding = 1e-9;
                int minX = (int)Math.Floor(start.Local.X + Math.Min(0, displacement.X) - radius - padding);
                int maxX = (int)Math.Floor(start.Local.X + Math.Max(0, displacement.X) + radius + padding);
                int minY = (int)Math.Floor(start.Local.Y + Math.Min(0, displacement.Y) - radius - padding);
                int maxY = (int)Math.Floor(start.Local.Y + Math.Max(0, displacement.Y) + radius + padding);
                int chunkMinX = FloorChunk(minX), chunkMaxX = FloorChunk(maxX);
                int chunkMinY = FloorChunk(minY), chunkMaxY = FloorChunk(maxY);
                for (int cy = chunkMinY; cy <= chunkMaxY; cy++) for (int cx = chunkMinX; cx <= chunkMaxX; cx++)
                {
                    var coord = new ChunkCoord(checked(start.Chunk.X + cx), checked(start.Chunk.Y + cy));
                    var chunk = world.GetChunk(coord);
                    if (!chunk.HasObstacles) continue;
                    int x0 = Math.Max(0, minX - cx * ChunkData.Size), x1 = Math.Min(31, maxX - cx * ChunkData.Size);
                    int y0 = Math.Max(0, minY - cy * ChunkData.Size), y1 = Math.Min(31, maxY - cy * ChunkData.Size);
                    uint mask = (uint.MaxValue << x0) & (uint.MaxValue >> (31 - x1));
                    for (int y = y0; y <= y1; y++)
                    {
                        uint row = chunk.BlockedRow(y) & mask;
                        if (row == 0) continue;
                        for (int x = x0; x <= x1; x++)
                            if ((row & (1U << x)) != 0) blockedCandidates.Add(new GridCell(coord, x, y));
                    }
                }
                return;
            }
            // Long, diagonal motion keeps the grid traversal rather than scanning a huge rectangle.
            foreach (var cell in CircleSweep.CandidateCells(start, displacement, radius))
                if (world.IsBlocked(cell)) blockedCandidates.Add(cell);
        }

        private static int FloorChunk(int cell) => (int)Math.Floor(cell / (double)ChunkData.Size);
    }
}
