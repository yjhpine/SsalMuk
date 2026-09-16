using System.Collections.Generic;

namespace SsalMuk.Core
{
    public enum PathStatus { Pending, Ready, NoPath }

    public sealed class PathRequest
    {
        private readonly ChunkRoutePlanner.Search chunks;
        private readonly NavigationGrid navigationGrid;
        private readonly System.Func<GridCell, double> risk;
        private readonly GridCell? from, to;
        private readonly WorldPosition target;
        private readonly List<WorldPosition> waypoints = new List<WorldPosition>();
        private GridAStar search;
        public PathStatus Status { get; private set; } = PathStatus.Pending;
        public IReadOnlyList<WorldPosition> Waypoints { get; }
        internal bool Cancelled { get; set; }
        public void Cancel() => Cancelled = true;
        internal PathRequest(WorldStore world, NavigationGrid grid, WorldPosition start, WorldPosition target, System.Func<GridCell, double> risk)
        {
            navigationGrid = grid; this.target = target; this.risk = risk; Waypoints = waypoints.AsReadOnly();
            from = grid.Anchor(start); to = grid.Anchor(target);
            if (!from.HasValue || !to.HasValue) { Status = PathStatus.NoPath; return; }
            chunks = new ChunkRoutePlanner(world).Request(from.Value.Chunk, to.Value.Chunk, grid.Radius);
        }
        internal int Advance(int budget)
        {
            if (Status != PathStatus.Pending || Cancelled) return 0;
            int used = chunks.Advance(budget);
            if (chunks.Status == PathStatus.NoPath) { Status = PathStatus.NoPath; return used; }
            if (chunks.Status != PathStatus.Ready || used == budget) return used;
            if (search == null)
            {
                var region = new HashSet<ChunkCoord>();
                foreach (var chunk in chunks.Route)
                    for (long y = -1; y <= 1; y++) for (long x = -1; x <= 1; x++)
                        region.Add(new ChunkCoord(checked(chunk.X + x), checked(chunk.Y + y)));
                search = new GridAStar(navigationGrid, from.Value, to.Value, region, risk);
            }
            used += search.Advance(budget - used);
            if (search.Status == PathStatus.Ready)
            {
                waypoints.AddRange(search.Route);
                if (waypoints.Count == 0 || waypoints[waypoints.Count - 1] != target) waypoints.Add(target);
                Status = PathStatus.Ready;
            }
            else if (search.Status == PathStatus.NoPath) Status = PathStatus.NoPath;
            return used;
        }
    }
}
