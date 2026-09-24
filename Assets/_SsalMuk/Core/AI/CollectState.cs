using System;
using System.Collections.Generic;
using System.Numerics;

namespace SsalMuk.Core
{
    public sealed class CollectState : IBehaviorState
    {
        private readonly Dictionary<long, Route> routes = new Dictionary<long, Route>();
        private readonly Dictionary<GridCell, Area> areasByCell = new Dictionary<GridCell, Area>();
        private readonly List<Area> areas = new List<Area>();
        private readonly Stack<Area> areaPool = new Stack<Area>();
        private readonly List<ItemCandidate> items = new List<ItemCandidate>();
        private readonly HashSet<long> validIds = new HashSet<long>();
        private readonly List<long> obsoleteRoutes = new List<long>();
        private readonly LootRecovery recovery;
        private ExperienceRecord selected;
        private PowerupRecord selectedItem;
        private double nextPlan, selectedAt;
        private int selectedPlan = -1, newPathBudget;
        private bool approachPending;
        public int PlanCount { get; private set; }
        public int AreaRiskEvaluations { get; private set; }

        public CollectState() : this(new LootRecovery()) { }
        internal CollectState(LootRecovery recovery) => this.recovery = recovery;
        public void Enter(AiContext context) { context.BreakoutDirection = DVec2.Zero; selectedPlan = -1; }
        public void Exit(AiContext context) => context.CollectionTargetId = null;

        internal void Observe(AiContext context)
        {
            if (context.Time + 1e-9 < nextPlan) return;
            nextPlan = context.Time + context.Settings.CollectionPlanSeconds; PlanCount++; newPathBudget = 8;
            foreach (var old in areas) { old.Records.Clear(); old.Accessible.Clear(); areaPool.Push(old); }
            areas.Clear(); areasByCell.Clear(); items.Clear(); validIds.Clear();
            foreach (long id in context.World.Query.QueryExperienceCircle(context.Actor.Position, context.Settings.SearchDistance)) Add(context, id);
            if (recovery.Active && recovery.TryGetTarget(context, out var remembered))
                foreach (long id in context.World.Query.QueryExperienceCircle(LootRecovery.AreaCenter(LootRecovery.AreaAt(remembered.Position)), 3)) Add(context, id);
            foreach (var area in areas)
            {
                FindAccessible(context, area);
                if (area.Value.IsZero) continue;
                var delta = context.Actor.Position.DisplacementTo(area.Representative.Position);
                // One enemy-risk evaluation per occupied area, never per orb.
                double approach = Math.Max(0, delta.Length - context.PickupRadius * .85);
                area.Cost = approach * context.Settings.DistanceWeight + context.RiskAt(delta.Normalized * approach) * context.Settings.RiskWeight;
                AreaRiskEvaluations++;
            }
            areas.Sort((a, b) =>
            {
                double difference = (double)(a.Value - b.Value) - (a.Cost - b.Cost);
                int order = difference > 0 ? -1 : difference < 0 ? 1 : 0;
                return order != 0 ? order : a.Representative.Id.CompareTo(b.Representative.Id);
            });
            foreach (long id in context.World.QueryItems(context.Actor.Position, context.Settings.SearchDistance))
            {
                if (!context.World.TryGetItem(id, out var item)) continue;
                validIds.Add(-id); var delta = context.Actor.Position.DisplacementTo(item.Position);
                items.Add(new ItemCandidate { Record = item, Cost = Math.Max(0, delta.Length - context.PickupRadius * .85) * context.Settings.DistanceWeight + context.RiskAt(delta) * context.Settings.RiskWeight });
            }
            items.Sort((a, b) => { int order = a.Cost.CompareTo(b.Cost); return order != 0 ? order : a.Record.Id.CompareTo(b.Record.Id); });
            obsoleteRoutes.Clear();
            foreach (var route in routes) if (!validIds.Contains(route.Key)) { route.Value.Request.Cancel(); obsoleteRoutes.Add(route.Key); }
            foreach (long id in obsoleteRoutes) routes.Remove(id);
            // Kills can first drop loot while emergency movement already controls the player.
            if (!recovery.Active && context.Actor is PlayerModel player && player.BrainState != BrainState.Collect)
            {
                Select(context);
                if (selected != null) { recovery.Remember(selected); recovery.Interrupt(context); }
            }
        }

        private void FindAccessible(AiContext context, Area area)
        {
            bool open = true;
            // An open area and its body-width margin form one connected patch. One route proves access to all its loot.
            int margin = Math.Max(1, (int)Math.Ceiling(context.Actor.BodyRadius));
            for (int y = -margin; y < 4 + margin && open; y++)
                for (int x = -margin; x < 4 + margin; x++)
                    if (context.World.IsBlocked(area.Cell.Offset(x, y))) { open = false; break; }
            if (open && TryApproach(context, area.Representative.Id, area.Representative.Position, out _))
            { area.Accessible.AddRange(area.Records); return; }
            area.Value = BigInteger.Zero; double nearest = double.PositiveInfinity;
            foreach (var xp in area.Records)
            {
                if (!TryApproach(context, xp.Id, xp.Position, out _)) continue;
                area.Accessible.Add(xp); area.Value += xp.Value;
                double distance = context.Actor.Position.DistanceTo(xp.Position);
                if (distance < nearest) { nearest = distance; area.Representative = xp; }
            }
        }

        private void Add(AiContext context, long id)
        {
            if (!context.World.TryGetExperience(id, out var xp) || xp.State != ExperienceState.Grounded || !validIds.Add(id)) return;
            var key = LootRecovery.AreaAt(xp.Position);
            if (!areasByCell.TryGetValue(key, out var area))
            {
                area = areaPool.Count > 0 ? areaPool.Pop() : new Area();
                area.Cell = key; area.Value = BigInteger.Zero; area.Representative = xp; area.Distance = double.PositiveInfinity;
                areasByCell.Add(key, area); areas.Add(area);
            }
            area.Value += xp.Value; area.Records.Add(xp);
            double distance = context.Actor.Position.DistanceTo(xp.Position);
            if (distance < area.Distance) { area.Distance = distance; area.Representative = xp; }
        }

        public void Tick(AiContext context, double dt)
        {
            context.MoveIntent = DVec2.Zero;
            if (recovery.Active && recovery.TryGetTarget(context, out var remembered))
            {
                context.CollectionTargetId = remembered.Id;
                if (recovery.TrySteer(context, out var tactical)) { context.MoveIntent = tactical; return; }
                if (TryApproach(context, remembered.Id, remembered.Position, out var returning)) { context.MoveIntent = returning; return; }
                // A remembered area must not suppress reachable alternatives indefinitely.
                if (approachPending) return;
                recovery.Clear(); selectedPlan = -1;
            }
            bool invalid = selected != null && !Grounded(context, selected) || selectedItem != null && !context.World.TryGetItem(selectedItem.Id, out _);
            if (selectedPlan != PlanCount || invalid) Select(context);
            if (selected != null && TryApproach(context, selected.Id, selected.Position, out var intent))
            { context.CollectionTargetId = selected.Id; context.MoveIntent = intent; recovery.Remember(selected); return; }
            context.CollectionTargetId = null;
            recovery.Clear();
            if (selectedItem != null && TryApproach(context, -selectedItem.Id, selectedItem.Position, out var itemIntent))
            { context.MoveIntent = itemIntent; return; }
            context.MoveIntent = context.EngageNearestEnemy();
        }

        private void Select(AiContext context)
        {
            var previous = selected; selected = null; selectedItem = null; selectedPlan = PlanCount;
            double itemCost = double.PositiveInfinity;
            foreach (var candidate in items)
                if (context.World.TryGetItem(candidate.Record.Id, out _) && TryApproach(context, -candidate.Record.Id, candidate.Record.Position, out _))
                { selectedItem = candidate.Record; itemCost = candidate.Cost; break; }
            if (previous != null && Grounded(context, previous) && context.Time - selectedAt < context.Settings.MinimumHoldSeconds)
            {
                int old = areas.FindIndex(area => area.Records.Contains(previous));
                if (old > 0) { var area = areas[old]; areas.RemoveAt(old); areas.Insert(0, area); }
            }
            int threshold = context.EngagementTarget?.Kind == UnitKind.Boss || selectedItem != null ? context.Settings.ExperiencePriorityThreshold : 1;
            int available = 0; double selectedCost = double.PositiveInfinity;
            foreach (var area in areas)
            {
                if (area.Value.IsZero) continue;
                ExperienceRecord reachable = null;
                if (Grounded(context, area.Representative) && TryApproach(context, area.Representative.Id, area.Representative.Position, out _)) reachable = area.Representative;
                if (reachable == null) foreach (var xp in area.Accessible)
                    if (xp != area.Representative && Grounded(context, xp) && TryApproach(context, xp.Id, xp.Position, out _)) { reachable = xp; break; }
                if (reachable == null) continue;
                if (selected == null) { selected = reachable; selectedCost = area.Cost; }
                foreach (var xp in area.Accessible)
                {
                    if (!Grounded(context, xp)) continue;
                    available += xp.Value >= threshold - available ? threshold - available : (int)xp.Value;
                    if (available >= threshold) break;
                }
                if (available >= threshold) break;
            }
            if (available < threshold || selectedCost > itemCost) selected = null;
            if (selected != null) { selectedItem = null; if (previous?.Id != selected.Id) selectedAt = context.Time; }
        }

        private static bool Grounded(AiContext context, ExperienceRecord xp) =>
            xp.State == ExperienceState.Grounded && context.World.TryGetExperience(xp.Id, out _);

        private bool TryApproach(AiContext context, long routeId, WorldPosition position, out DVec2 intent)
        {
            intent = DVec2.Zero; approachPending = false; var actor = context.Actor;
            var delta = actor.Position.DisplacementTo(position);
            if (delta.Length <= context.PickupRadius * .9) return true;
            var target = position.Offset(-delta.Normalized * (context.PickupRadius * .85));
            var direct = actor.Position.DisplacementTo(target);
            if (direct.Length > context.Settings.SearchDistance)
            {
                // Remember the original loot, but inspect/navigate only a local step toward it.
                target = actor.Position.Offset(direct.Normalized * context.Settings.SearchDistance);
                if (!context.World.Query.IsCircleFree(target, actor.BodyRadius))
                {
                    var side = new DVec2(-direct.Y, direct.X).Normalized * context.Settings.ProbeDistance;
                    if (context.World.Query.IsCircleFree(target.Offset(side), actor.BodyRadius)) target = target.Offset(side);
                    else if (context.World.Query.IsCircleFree(target.Offset(-side), actor.BodyRadius)) target = target.Offset(-side);
                }
                direct = actor.Position.DisplacementTo(target);
            }
            if (!context.World.Query.IsCircleFree(target, actor.BodyRadius)) return false;
            if (!context.World.Query.SweepCircle(actor.Position, direct, actor.BodyRadius).HasValue)
            { intent = direct.Normalized; return true; }
            if (!routes.TryGetValue(routeId, out var route) || route.Revision != context.World.TerrainRevision)
            {
                if (newPathBudget <= 0) { approachPending = true; return false; }
                newPathBudget--; route?.Request?.Cancel();
                route = new Route { Revision = context.World.TerrainRevision, Request = context.Navigation.RequestPath(actor.Position, target, actor.BodyRadius) };
                routes[routeId] = route;
            }
            if (route.Request.Status != PathStatus.Ready) { approachPending = route.Request.Status == PathStatus.Pending; return false; }
            while (route.Index < route.Request.Waypoints.Count && actor.Position.DistanceTo(route.Request.Waypoints[route.Index]) < .09) route.Index++;
            if (route.Index >= route.Request.Waypoints.Count) { routes.Remove(routeId); approachPending = true; return false; }
            var step = actor.Position.DisplacementTo(route.Request.Waypoints[route.Index]);
            if (context.World.Query.SweepCircle(actor.Position, step, actor.BodyRadius).HasValue)
            { route.Request.Cancel(); routes.Remove(routeId); approachPending = true; return false; }
            intent = step.Normalized; return true;
        }
        private sealed class Route { public long Revision; public PathRequest Request; public int Index; }
        private sealed class Area
        {
            public readonly List<ExperienceRecord> Records = new List<ExperienceRecord>();
            public readonly List<ExperienceRecord> Accessible = new List<ExperienceRecord>();
            public GridCell Cell; public ExperienceRecord Representative; public BigInteger Value; public double Distance, Cost;
        }
        private struct ItemCandidate { public PowerupRecord Record; public double Cost; }
    }
}
