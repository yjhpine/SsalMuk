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
        private readonly Dictionary<double, FlowField> previousFields = new Dictionary<double, FlowField>();
        private readonly List<PathRequest> requests = new List<PathRequest>();
        private readonly Dictionary<long, ChaseRoute> chases = new Dictionary<long, ChaseRoute>();
        private readonly Dictionary<long, DirectionDecision> directions = new Dictionary<long, DirectionDecision>();
        private int jobCursor;
        private long advances;
        private double elapsedSeconds, directionDecisionSeconds;
        public int PendingRequestCount
        { get { int count = 0; foreach (var request in requests) if (request.Status == PathStatus.Pending && !request.Cancelled) count++; return count; } }
        public long OldestPendingSteps
        { get { long age = 0; foreach (var request in requests) if (request.Status == PathStatus.Pending && !request.Cancelled) age = Math.Max(age, advances - request.RequestedAtAdvance); return age; } }
        public NavigationService(WorldStore world, Func<GridCell, double> risk = null)
        { this.world = world ?? throw new ArgumentNullException(nameof(world)); this.risk = risk; }
        internal void ConfigureDirectionCadence(double seconds)
        {
            if (seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            directionDecisionSeconds = seconds; directions.Clear();
        }
        internal void AdvanceTime(double seconds)
        {
            if (seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            elapsedSeconds += seconds;
        }
        internal void InvalidateDirection(long id) => directions.Remove(id);
        private NavigationGrid Grid(double radius)
        {
            SpatialIndex.RequireRadius(radius);
            if (!grids.TryGetValue(radius, out var grid)) grids.Add(radius, grid = new NavigationGrid(world, radius));
            return grid;
        }
        public PathRequest RequestPath(WorldPosition from, WorldPosition to, double radius)
        {
            var request = new PathRequest(world, Grid(radius), from, to, risk);
            request.RequestedAtAdvance = advances;
            if (request.Status == PathStatus.Pending) requests.Add(request); return request;
        }
        public FlowField GetFlowField(WorldPosition target, double radius)
        {
            var cell = GridCell.At(target);
            if (!fields.TryGetValue(radius, out var field) || !field.Target.Equals(cell) || field.TerrainRevision != world.TerrainRevision)
            {
                if (field != null && field.TerrainRevision == world.TerrainRevision)
                {
                    if (field.IsComplete || !previousFields.ContainsKey(radius)) previousFields[radius] = field;
                }
                else previousFields.Remove(radius);
                fields[radius] = field = new FlowField(Grid(radius), cell, world.TerrainRevision);
            }
            return field;
        }
        public void Advance(int nodeBudget = 512)
        {
            if (nodeBudget <= 0) throw new ArgumentOutOfRangeException(nameof(nodeBudget));
            advances = checked(advances + 1);
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
            if (directionDecisionSeconds <= 0 || (unit.Kind != UnitKind.Normal && unit.Kind != UnitKind.Boss))
                return ComputeChaseDirection(unit, target, out _);
            var targetCell = GridCell.At(target);
            if (directions.TryGetValue(unit.Id, out var cached) && cached.Revision == world.TerrainRevision &&
                cached.Target.Equals(targetCell) && elapsedSeconds < cached.RefreshAt && !RouteChanged(unit.Id) &&
                !ApproachingDecisionPoint(unit, target, cached))
                return cached.Direction;
            var direction = ComputeChaseDirection(unit, target, out var aim);
            directions[unit.Id] = new DirectionDecision(targetCell, world.TerrainRevision, direction, aim,
                elapsedSeconds + StaggeredInterval(unit.Id));
            return direction;
        }
        private DVec2 ComputeChaseDirection(UnitModel unit, WorldPosition target, out WorldPosition? aim)
        {
            aim = null;
            if (chases.TryGetValue(unit.Id, out var existing)) UseCompletedRoute(existing);
            var delta = unit.Position.DisplacementTo(target);
            if (delta.Length < 1e-6) return DVec2.Zero;
            if (delta.Length < 2 && !world.Query.SweepCircle(unit.Position, delta, unit.BodyRadius).HasValue)
            { aim = target; return delta.Normalized; }
            var field = GetFlowField(target, unit.BodyRadius); var cell = GridCell.At(unit.Position);
            if (field.Contains(cell))
            {
                if (TryFlowDirection(unit, field, target, out var direction, out var flowAim)) { aim = flowAim; return direction; }
                if (!field.IsComplete)
                {
                    if (previousFields.TryGetValue(unit.BodyRadius, out var previous) &&
                        TryFlowDirection(unit, previous, previous.Target.Center, out direction, out flowAim))
                    { aim = flowAim; return direction; }
                    return PreviousDirection(unit, target, out aim);
                }
            }
            if (!chases.TryGetValue(unit.Id, out var chase)) chases.Add(unit.Id, chase = new ChaseRoute());
            UseCompletedRoute(chase);
            var targetCell = GridCell.At(target);
            if (chase.Request == null || chase.Revision != world.TerrainRevision ||
                (!chase.Target.Equals(targetCell) && chase.Request.Status != PathStatus.Pending))
            {
                if (chase.Request != null) chase.Request.Cancelled = true;
                chase.Target = targetCell; chase.Revision = world.TerrainRevision;
                chase.Request = RequestPath(unit.Position, target, unit.BodyRadius);
            }
            UseCompletedRoute(chase);
            return PreviousDirection(unit, target, out aim);
        }
        private bool ApproachingDecisionPoint(UnitModel unit, WorldPosition target, DirectionDecision cached)
        {
            double threshold = Math.Max(0.08, unit.Definition.MoveSpeed * directionDecisionSeconds * 1.25);
            if (unit.Position.DistanceTo(target) <= threshold) return true;
            if (!cached.Aim.HasValue) return false;
            var remaining = unit.Position.DisplacementTo(cached.Aim.Value);
            return remaining.Length <= threshold || (cached.Direction != DVec2.Zero && DVec2.Dot(remaining, cached.Direction) <= 0);
        }
        private bool RouteChanged(long id) => chases.TryGetValue(id, out var chase) && chase.Request != null &&
            chase.Request.Status == PathStatus.Ready && !ReferenceEquals(chase.Route, chase.Request.Waypoints);
        private double StaggeredInterval(long id)
        {
            ulong bits = StableHash.Combine(83, id, 0, 1);
            double phase = (bits & 65535) / 65535.0;
            return directionDecisionSeconds * (0.8 + phase * 0.4);
        }
        private bool TryFlowDirection(UnitModel unit, FlowField field, WorldPosition target, out DVec2 direction, out WorldPosition aim)
        {
            direction = DVec2.Zero; aim = default;
            var cell = GridCell.At(unit.Position);
            if (field.TerrainRevision != world.TerrainRevision || !field.Contains(cell) || !field.TryGetNext(cell, out var next)) return false;
            var desiredAim = next.Equals(field.Target) ? target : next.Center;
            var delta = unit.Position.DisplacementTo(desiredAim);
            if (!world.Query.SweepCircle(unit.Position, delta, unit.BodyRadius).HasValue)
            { direction = delta.Normalized; aim = desiredAim; return true; }
            var anchor = Grid(unit.BodyRadius).Anchor(unit.Position);
            if (!anchor.HasValue || unit.Position.DistanceTo(anchor.Value.Center) <= 0.05) return false;
            aim = anchor.Value.Center; direction = unit.Position.DisplacementTo(aim).Normalized; return true;
        }
        private static void UseCompletedRoute(ChaseRoute chase)
        {
            if (chase.Request != null && chase.Request.Status == PathStatus.Ready && !ReferenceEquals(chase.Route, chase.Request.Waypoints))
            { chase.Route = chase.Request.Waypoints; chase.Index = 0; }
        }
        private DVec2 PreviousDirection(UnitModel unit, WorldPosition target, out WorldPosition? aim)
        {
            aim = null;
            if (!chases.TryGetValue(unit.Id, out var chase) || chase.Route == null) return DVec2.Zero;
            while (chase.Index < chase.Route.Count && unit.Position.DistanceTo(chase.Route[chase.Index]) < 0.08) chase.Index++;
            if (chase.Index >= chase.Route.Count) return DVec2.Zero;
            // Shared grid centers are route guides, not mandatory stopping points.
            // A crowd can block a starting waypoint behind a body even though the next segment is clear.
            int furthest = chase.Index;
            for (int candidate = chase.Index + 1; candidate < Math.Min(chase.Route.Count, chase.Index + 9); candidate++)
            {
                var lookAhead = unit.Position.DisplacementTo(chase.Route[candidate]);
                if (!world.Query.SweepCircle(unit.Position, lookAhead, unit.BodyRadius).HasValue) furthest = candidate;
            }
            chase.Index = furthest;
            var delta = unit.Position.DisplacementTo(chase.Route[chase.Index]);
            if (world.Query.SweepCircle(unit.Position, delta, unit.BodyRadius).HasValue) return DVec2.Zero;
            aim = chase.Route[chase.Index]; return delta.Normalized;
        }
        public void ForgetUnit(long id)
        {
            directions.Remove(id);
            if (chases.TryGetValue(id, out var chase))
            {
                if (chase.Request != null) chase.Request.Cancelled = true; chases.Remove(id);
            }
        }
        private sealed class ChaseRoute
        {
            public GridCell Target; public long Revision; public PathRequest Request;
            public IReadOnlyList<WorldPosition> Route; public int Index;
        }
        private readonly struct DirectionDecision
        {
            public GridCell Target { get; }
            public long Revision { get; }
            public DVec2 Direction { get; }
            public WorldPosition? Aim { get; }
            public double RefreshAt { get; }
            public DirectionDecision(GridCell target, long revision, DVec2 direction, WorldPosition? aim, double refreshAt)
            { Target = target; Revision = revision; Direction = direction; Aim = aim; RefreshAt = refreshAt; }
        }
    }
}
