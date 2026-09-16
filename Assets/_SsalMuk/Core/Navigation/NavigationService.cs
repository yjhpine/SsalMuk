using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    public sealed class NavigationService : INavigation
    {
        private readonly WorldStore world;
        private readonly Func<GridCell, double> risk;
        private readonly Dictionary<double, NavigationGrid> grids = new Dictionary<double, NavigationGrid>();
        private readonly Dictionary<double, FlowField> fields = new Dictionary<double, FlowField>();
        private readonly List<PathRequest> requests = new List<PathRequest>();
        private readonly Dictionary<long, ChaseRoute> chases = new Dictionary<long, ChaseRoute>();
        private int jobCursor;
        public NavigationService(WorldStore world, Func<GridCell, double> risk = null)
        { this.world = world ?? throw new ArgumentNullException(nameof(world)); this.risk = risk; }
        private NavigationGrid Grid(double radius)
        {
            SpatialIndex.RequireRadius(radius);
            if (!grids.TryGetValue(radius, out var grid)) grids.Add(radius, grid = new NavigationGrid(world, radius));
            return grid;
        }
        public PathRequest RequestPath(WorldPosition from, WorldPosition to, double radius)
        {
            var request = new PathRequest(world, Grid(radius), from, to, risk);
            if (request.Status == PathStatus.Pending) requests.Add(request); return request;
        }
        public FlowField GetFlowField(WorldPosition target, double radius)
        {
            var cell = GridCell.At(target);
            if (!fields.TryGetValue(radius, out var field) || !field.Target.Equals(cell) || field.TerrainRevision != world.TerrainRevision)
                fields[radius] = field = new FlowField(Grid(radius), cell, world.TerrainRevision);
            return field;
        }
        public void Advance(int nodeBudget = 512)
        {
            if (nodeBudget <= 0) throw new ArgumentOutOfRangeException(nameof(nodeBudget));
            var jobs = new List<Func<int, int>>();
            foreach (var field in fields.Values) if (!field.IsComplete) jobs.Add(field.Advance);
            foreach (var request in requests) if (request.Status == PathStatus.Pending && !request.Cancelled) jobs.Add(request.Advance);
            if (jobs.Count == 0) return;
            while (nodeBudget > 0)
            {
                int allotment = Math.Min(32, nodeBudget); jobCursor %= jobs.Count;
                jobs[jobCursor++](allotment); nodeBudget -= allotment;
            }
            requests.RemoveAll(request => request.Status != PathStatus.Pending || request.Cancelled);
        }

        public DVec2 ChaseDirection(UnitModel unit, WorldPosition target)
        {
            var delta = unit.Position.DisplacementTo(target);
            if (delta.Length < 1e-6) return DVec2.Zero;
            if (delta.Length < 2 && !world.Query.SweepCircle(unit.Position, delta, unit.BodyRadius).HasValue) return delta.Normalized;
            var field = GetFlowField(target, unit.BodyRadius); var cell = GridCell.At(unit.Position);
            if (field.Contains(cell))
            {
                if (field.TryGetNext(cell, out var next))
                {
                    var aim = next.Equals(field.Target) ? target : next.Center;
                    var direction = unit.Position.DisplacementTo(aim);
                    if (!world.Query.SweepCircle(unit.Position, direction, unit.BodyRadius).HasValue) return direction.Normalized;
                    var anchor = Grid(unit.BodyRadius).Anchor(unit.Position);
                    if (anchor.HasValue && unit.Position.DistanceTo(anchor.Value.Center) > 0.05)
                        return unit.Position.DisplacementTo(anchor.Value.Center).Normalized;
                }
                if (!field.IsComplete) return PreviousDirection(unit, target);
            }
            if (!chases.TryGetValue(unit.Id, out var chase)) chases.Add(unit.Id, chase = new ChaseRoute());
            var targetCell = GridCell.At(target);
            if (chase.Request == null || !chase.Target.Equals(targetCell) || chase.Revision != world.TerrainRevision)
            {
                if (chase.Request != null) chase.Request.Cancelled = true;
                chase.Target = targetCell; chase.Revision = world.TerrainRevision;
                chase.Request = RequestPath(unit.Position, target, unit.BodyRadius);
            }
            if (chase.Request.Status == PathStatus.Ready && !ReferenceEquals(chase.Route, chase.Request.Waypoints))
            { chase.Route = chase.Request.Waypoints; chase.Index = 0; }
            return PreviousDirection(unit, target);
        }
        private DVec2 PreviousDirection(UnitModel unit, WorldPosition target)
        {
            if (!chases.TryGetValue(unit.Id, out var chase) || chase.Route == null) return DVec2.Zero;
            while (chase.Index < chase.Route.Count && unit.Position.DistanceTo(chase.Route[chase.Index]) < 0.08) chase.Index++;
            if (chase.Index >= chase.Route.Count) return DVec2.Zero;
            var delta = unit.Position.DisplacementTo(chase.Route[chase.Index]);
            if (world.Query.SweepCircle(unit.Position, delta, unit.BodyRadius).HasValue) return DVec2.Zero;
            return delta.Normalized;
        }
        public void ForgetUnit(long id)
        {
            if (!chases.TryGetValue(id, out var chase)) return;
            if (chase.Request != null) chase.Request.Cancelled = true; chases.Remove(id);
        }
        private sealed class ChaseRoute
        {
            public GridCell Target; public long Revision; public PathRequest Request;
            public IReadOnlyList<WorldPosition> Route; public int Index;
        }
    }
}
