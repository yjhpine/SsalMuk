using System.Collections.Generic;

namespace SsalMuk.Core
{
    public sealed class FlowField
    {
        public const int HalfExtent = 12;
        private readonly NavigationGrid grid;
        private readonly SearchQueue<GridCell> open = new SearchQueue<GridCell>();
        private readonly Dictionary<GridCell, double> distances = new Dictionary<GridCell, double>();
        private readonly Dictionary<GridCell, GridCell> nextCells = new Dictionary<GridCell, GridCell>();
        private readonly HashSet<GridCell> settled = new HashSet<GridCell>();
        public GridCell Target { get; }
        public long TerrainRevision { get; }
        public bool IsComplete { get; private set; }
        internal FlowField(NavigationGrid grid, GridCell target, long terrainRevision)
        {
            this.grid = grid; Target = target; TerrainRevision = terrainRevision;
            if (!grid.IsWalkable(target)) { IsComplete = true; return; }
            distances[target] = 0; open.Push(target, 0);
        }
        public bool Contains(GridCell cell)
        {
            var delta = Target.Center.DisplacementTo(cell.Center);
            return System.Math.Abs(delta.X) <= HalfExtent && System.Math.Abs(delta.Y) <= HalfExtent;
        }
        public bool TryGetNext(GridCell cell, out GridCell next)
        {
            next = default;
            if (!settled.Contains(cell)) return false;
            if (cell.Equals(Target)) { next = cell; return true; }
            return nextCells.TryGetValue(cell, out next);
        }
        internal int Advance(int budget)
        {
            int used = 0;
            while (open.Count > 0 && used < budget)
            {
                used++; var cell = open.Pop(); if (!settled.Add(cell)) continue;
                foreach (var direction in NavigationGrid.Directions)
                {
                    var next = cell.Offset(direction.x, direction.y);
                    if (!Contains(next) || settled.Contains(next) || !grid.CanStep(cell, next)) continue;
                    double distance = distances[cell] + (direction.x == 0 || direction.y == 0 ? 1 : System.Math.Sqrt(2));
                    if (distances.TryGetValue(next, out double previous) && previous <= distance) continue;
                    distances[next] = distance; nextCells[next] = cell; open.Push(next, distance);
                }
            }
            IsComplete = open.Count == 0; return used;
        }
    }
}
