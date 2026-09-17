using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    public sealed class SpatialIndex
    {
        private readonly Dictionary<long, WorldPosition> positions = new Dictionary<long, WorldPosition>();
        private readonly Dictionary<GridCell, SortedSet<long>> cells = new Dictionary<GridCell, SortedSet<long>>();
        private readonly Dictionary<ChunkCoord, HashSet<GridCell>> chunks = new Dictionary<ChunkCoord, HashSet<GridCell>>();
        public int OccupiedChunkCount => chunks.Count;

        public void Upsert(long id, WorldPosition position)
        {
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
            var cell = GridCell.At(position);
            if (positions.TryGetValue(id, out var previous))
            {
                if (GridCell.At(previous).Equals(cell)) { positions[id] = position; return; }
                Remove(id);
            }
            positions[id] = position;
            if (!cells.TryGetValue(cell, out var ids))
            {
                cells.Add(cell, ids = new SortedSet<long>());
                if (!chunks.TryGetValue(cell.Chunk, out var region)) chunks.Add(cell.Chunk, region = new HashSet<GridCell>());
                region.Add(cell);
            }
            ids.Add(id);
        }

        public bool Remove(long id)
        {
            if (!positions.TryGetValue(id, out var position)) return false;
            var cell = GridCell.At(position); positions.Remove(id);
            var ids = cells[cell]; ids.Remove(id);
            if (ids.Count == 0)
            {
                cells.Remove(cell); var region = chunks[cell.Chunk]; region.Remove(cell);
                if (region.Count == 0) chunks.Remove(cell.Chunk);
            }
            return true;
        }

        public long? FindNearest(WorldPosition origin, Func<long, bool> accepts = null)
        {
            var regions = new List<(ChunkCoord coord, double distance)>();
            foreach (var chunk in chunks.Keys) regions.Add((chunk, LowerBound(origin, new WorldPosition(chunk, DVec2.Zero), 32)));
            regions.Sort((a, b) => a.distance.CompareTo(b.distance));
            double bestDistance = double.PositiveInfinity; long? bestId = null;
            foreach (var region in regions)
            {
                if (region.distance > bestDistance) break;
                foreach (var cell in chunks[region.coord])
                {
                    if (LowerBound(origin, new WorldPosition(cell.Chunk, new DVec2(cell.X, cell.Y)), 1) > bestDistance) continue;
                    foreach (long id in cells[cell])
                    {
                        if (accepts != null && !accepts(id)) continue;
                        double distance = origin.DistanceTo(positions[id]);
                        if (distance < bestDistance || (distance == bestDistance && (!bestId.HasValue || id < bestId.Value)))
                        { bestDistance = distance; bestId = id; }
                    }
                }
            }
            return bestId;
        }

        public IReadOnlyList<long> QueryCircle(WorldPosition origin, double radius, Func<long, bool> accepts = null)
        {
            var result = new List<long>(); QueryCircle(origin, radius, result, accepts); return result.AsReadOnly();
        }

        public void QueryCircle(WorldPosition origin, double radius, List<long> result, Func<long, bool> accepts = null)
        {
            RequireRadius(radius);
            if (result == null) throw new ArgumentNullException(nameof(result));
            result.Clear();
            if (radius <= WorldPosition.ChunkSize && chunks.Count > 9)
            {
                // A local circle touches at most the surrounding nine chunks. Far living entities
                // remain indexed; their growing count need not make every local query scan them.
                int minChunkX = origin.Local.X - radius <= 0 ? -1 : 0;
                int maxChunkX = origin.Local.X + radius >= WorldPosition.ChunkSize ? 1 : 0;
                int minChunkY = origin.Local.Y - radius <= 0 ? -1 : 0;
                int maxChunkY = origin.Local.Y + radius >= WorldPosition.ChunkSize ? 1 : 0;
                for (int y = minChunkY; y <= maxChunkY; y++) for (int x = minChunkX; x <= maxChunkX; x++)
                {
                    if ((x < 0 && origin.Chunk.X == long.MinValue) || (x > 0 && origin.Chunk.X == long.MaxValue) ||
                        (y < 0 && origin.Chunk.Y == long.MinValue) || (y > 0 && origin.Chunk.Y == long.MaxValue)) continue;
                    var coord = new ChunkCoord(origin.Chunk.X + x, origin.Chunk.Y + y);
                    if (chunks.TryGetValue(coord, out var region)) Collect(coord, region);
                }
            }
            else foreach (var chunk in chunks) Collect(chunk.Key, chunk.Value);
            result.Sort();

            void Collect(ChunkCoord coord, HashSet<GridCell> region)
            {
                if (LowerBound(origin, new WorldPosition(coord, DVec2.Zero), 32) > radius) return;
                if (radius <= WorldPosition.ChunkSize)
                {
                    var local = new WorldPosition(coord, DVec2.Zero).DisplacementTo(origin);
                    // Include one extra cell at each edge so floating-point boundary rounding
                    // cannot narrow the broad phase. Exact distances still decide membership.
                    int minX = Math.Max(0, (int)Math.Floor(local.X - radius) - 1);
                    int maxX = Math.Min(31, (int)Math.Floor(local.X + radius) + 1);
                    int minY = Math.Max(0, (int)Math.Floor(local.Y - radius) - 1);
                    int maxY = Math.Min(31, (int)Math.Floor(local.Y + radius) + 1);
                    if (minX > maxX || minY > maxY) return;
                    if ((maxX - minX + 1) * (maxY - minY + 1) < region.Count)
                    {
                        for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++)
                            if (cells.TryGetValue(new GridCell(coord, x, y), out var ids)) CollectIds(ids);
                        return;
                    }
                }
                foreach (var cell in region)
                {
                    if (LowerBound(origin, new WorldPosition(cell.Chunk, new DVec2(cell.X, cell.Y)), 1) > radius) continue;
                    CollectIds(cells[cell]);
                }
            }
            void CollectIds(SortedSet<long> ids)
            {
                foreach (long id in ids)
                    if ((accepts == null || accepts(id)) && origin.DistanceTo(positions[id]) <= radius) result.Add(id);
            }
        }

        private static double LowerBound(WorldPosition origin, WorldPosition minimum, double size)
        {
            var delta = origin.DisplacementTo(minimum);
            return new DVec2(Math.Max(0, Math.Max(delta.X, -delta.X - size)), Math.Max(0, Math.Max(delta.Y, -delta.Y - size))).Length;
        }
        internal static void RequireRadius(double radius)
        {
            if (radius < 0 || double.IsNaN(radius) || double.IsInfinity(radius)) throw new ArgumentOutOfRangeException(nameof(radius));
        }
        internal void Clear() { positions.Clear(); cells.Clear(); chunks.Clear(); }
    }
}
