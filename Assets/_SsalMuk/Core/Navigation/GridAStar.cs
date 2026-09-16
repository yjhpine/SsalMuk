using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    public sealed class GridAStar
    {
        private readonly NavigationGrid grid;
        private readonly Func<GridCell, double> risk;
        private readonly GridCell from, target;
        private readonly HashSet<ChunkCoord> region;
        private readonly SearchQueue<GridCell> open = new SearchQueue<GridCell>();
        private readonly Dictionary<GridCell, double> costs = new Dictionary<GridCell, double>();
        private readonly Dictionary<GridCell, GridCell> parents = new Dictionary<GridCell, GridCell>();
        private readonly HashSet<GridCell> closed = new HashSet<GridCell>();
        public PathStatus Status { get; private set; } = PathStatus.Pending;
        internal List<WorldPosition> Route { get; } = new List<WorldPosition>();
        internal GridAStar(NavigationGrid grid, GridCell from, GridCell target, HashSet<ChunkCoord> region, Func<GridCell, double> risk)
        {
            this.grid = grid; this.from = from; this.target = target; this.region = region; this.risk = risk;
            costs[from] = 0; open.Push(from, from.Center.DistanceTo(target.Center));
        }
        internal int Advance(int budget)
        {
            int used = 0;
            while (Status == PathStatus.Pending && open.Count > 0 && used < budget)
            {
                used++; var cell = open.Pop(); if (!closed.Add(cell)) continue;
                if (cell.Equals(target))
                {
                    while (!cell.Equals(from)) { Route.Add(cell.Center); cell = parents[cell]; }
                    Route.Add(from.Center); Route.Reverse(); Status = PathStatus.Ready; break;
                }
                foreach (var direction in NavigationGrid.Directions)
                {
                    var next = cell.Offset(direction.x, direction.y);
                    if (!region.Contains(next.Chunk) || closed.Contains(next) || !grid.CanStep(cell, next)) continue;
                    double penalty = risk == null ? 0 : risk(next);
                    if (penalty < 0 || double.IsNaN(penalty) || double.IsInfinity(penalty)) throw new ArgumentOutOfRangeException(nameof(risk), "Navigation risk cost must be nonnegative and finite.");
                    double cost = costs[cell] + (direction.x == 0 || direction.y == 0 ? 1 : Math.Sqrt(2)) + penalty;
                    if (costs.TryGetValue(next, out double previous) && previous <= cost) continue;
                    costs[next] = cost; parents[next] = cell; double h = next.Center.DistanceTo(target.Center); open.Push(next, cost + h, h);
                }
            }
            if (Status == PathStatus.Pending && open.Count == 0) Status = PathStatus.NoPath;
            return used;
        }
    }
}
