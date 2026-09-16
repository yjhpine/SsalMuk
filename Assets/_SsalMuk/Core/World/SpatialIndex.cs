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
            RequireRadius(radius); var result = new List<long>();
            foreach (var chunk in chunks)
            {
                if (LowerBound(origin, new WorldPosition(chunk.Key, DVec2.Zero), 32) > radius) continue;
                foreach (var cell in chunk.Value)
                {
                    if (LowerBound(origin, new WorldPosition(cell.Chunk, new DVec2(cell.X, cell.Y)), 1) > radius) continue;
                    foreach (long id in cells[cell])
                        if ((accepts == null || accepts(id)) && origin.DistanceTo(positions[id]) <= radius) result.Add(id);
                }
            }
            result.Sort(); return result.AsReadOnly();
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
