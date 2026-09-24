using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    public sealed class CollectState : IBehaviorState
    {
        private readonly Dictionary<long, Route> routes = new Dictionary<long, Route>();
        private double selectedAt;
        public void Enter(AiContext context) { context.BreakoutDirection = DVec2.Zero; selectedAt = double.NegativeInfinity; }
        public void Exit(AiContext context)
        {
            context.CollectionTargetId = null;
            foreach (var route in routes.Values) route.Request?.Cancel();
            routes.Clear();
        }

        public void Tick(AiContext context, double dt)
        {
            context.MoveIntent = DVec2.Zero;
            var candidates = new List<Candidate>(); var validIds = new HashSet<long>();
            var items = new List<ItemCandidate>();
            foreach (long id in context.World.Query.QueryExperienceCircle(context.Actor.Position, context.Settings.SearchDistance))
            {
                if (!context.World.TryGetExperience(id, out var xp) || xp.State != ExperienceState.Grounded) continue;
                validIds.Add(id);
                var delta = context.Actor.Position.DisplacementTo(xp.Position);
                double cost = delta.Length * context.Settings.DistanceWeight + context.RiskAt(delta) * context.Settings.RiskWeight;
                candidates.Add(new Candidate { Record = xp, Cost = cost });
            }
            foreach (long id in context.World.QueryItems(context.Actor.Position, context.Settings.SearchDistance))
            {
                if (!context.World.TryGetItem(id, out var item)) continue;
                validIds.Add(-id);
                var delta = context.Actor.Position.DisplacementTo(item.Position);
                double cost = delta.Length * context.Settings.DistanceWeight + context.RiskAt(delta) * context.Settings.RiskWeight;
                items.Add(new ItemCandidate { Record = item, Cost = cost });
            }
            foreach (long id in new List<long>(routes.Keys)) if (!validIds.Contains(id)) { routes[id].Request?.Cancel(); routes.Remove(id); }
            candidates.Sort((a, b) =>
            {
                double difference = (double)(a.Record.Value - b.Record.Value) - (a.Cost - b.Cost);
                int order = difference > 0 ? -1 : difference < 0 ? 1 : 0;
                return order != 0 ? order : a.Record.Id.CompareTo(b.Record.Id);
            });
            if (context.CollectionTargetId.HasValue && context.Time - selectedAt < context.Settings.MinimumHoldSeconds)
            {
                int old = candidates.FindIndex(candidate => candidate.Record.Id == context.CollectionTargetId.Value);
                if (old > 0) { var candidate = candidates[old]; candidates.RemoveAt(old); candidates.Insert(0, candidate); }
            }
            items.Sort((a, b) => { int order = a.Cost.CompareTo(b.Cost); return order != 0 ? order : a.Record.Id.CompareTo(b.Record.Id); });
            var itemIntent = DVec2.Zero; bool itemSelected = false; double itemCost = double.PositiveInfinity;
            foreach (var candidate in items)
            {
                if (!TryApproach(context, -candidate.Record.Id, candidate.Record.Position, out itemIntent)) continue;
                itemSelected = true; itemCost = candidate.Cost; break;
            }
            bool bossPriority = context.EngagementTarget?.Kind == UnitKind.Boss;
            int threshold = bossPriority || itemSelected ? context.Settings.ExperiencePriorityThreshold : 1;
            int available = 0; ExperienceRecord selected = null; var selectedIntent = DVec2.Zero;
            double selectedCost = double.PositiveInfinity;
            foreach (var candidate in candidates)
            {
                if (!TryApproach(context, candidate.Record.Id, candidate.Record.Position, out var intent)) continue;
                if (selected == null) { selected = candidate.Record; selectedIntent = intent; selectedCost = candidate.Cost; }
                // Cap the count before conversion so arbitrarily large XP values remain safe.
                available += candidate.Record.Value >= threshold - available ? threshold - available : (int)candidate.Record.Value;
                if (available >= threshold) break;
            }
            // Useful nearby items win sparse loot; a closer, abundant XP route can still win.
            if (selected != null && available >= threshold && (!itemSelected || selectedCost <= itemCost))
            {
                if (context.CollectionTargetId != selected.Id) selectedAt = context.Time;
                context.CollectionTargetId = selected.Id; context.MoveIntent = selectedIntent; return;
            }
            context.CollectionTargetId = null;
            if (itemSelected) { context.MoveIntent = itemIntent; return; }
            context.MoveIntent = context.EngageNearestEnemy();
        }

        private bool TryApproach(AiContext context, long routeId, WorldPosition position, out DVec2 intent)
        {
            intent = DVec2.Zero; var actor = context.Actor;
            var delta = actor.Position.DisplacementTo(position);
            if (delta.Length <= context.PickupRadius * 0.9) return true;
            // Aim just inside the pickup radius. Orbs on walls can be collected from a safe edge.
            var target = position.Offset(-delta.Normalized * (context.PickupRadius * 0.85));
            if (!context.World.Query.IsCircleFree(target, actor.BodyRadius)) return false;
            var direct = actor.Position.DisplacementTo(target);
            if (!context.World.Query.SweepCircle(actor.Position, direct, actor.BodyRadius).HasValue)
            { intent = direct.Normalized; return true; }
            if (!routes.TryGetValue(routeId, out var route) || route.Revision != context.World.TerrainRevision)
            {
                route?.Request?.Cancel();
                route = new Route { Revision = context.World.TerrainRevision, Request = context.Navigation.RequestPath(actor.Position, target, actor.BodyRadius) };
                routes[routeId] = route;
            }
            if (route.Request.Status != PathStatus.Ready) return false;
            while (route.Index < route.Request.Waypoints.Count && actor.Position.DistanceTo(route.Request.Waypoints[route.Index]) < 0.09) route.Index++;
            if (route.Index >= route.Request.Waypoints.Count) { routes.Remove(routeId); return false; }
            var step = actor.Position.DisplacementTo(route.Request.Waypoints[route.Index]);
            if (context.World.Query.SweepCircle(actor.Position, step, actor.BodyRadius).HasValue)
            { route.Request.Cancel(); routes.Remove(routeId); return false; }
            intent = step.Normalized; return true;
        }
        private sealed class Route { public long Revision; public PathRequest Request; public int Index; }
        private struct Candidate { public ExperienceRecord Record; public double Cost; }
        private struct ItemCandidate { public PowerupRecord Record; public double Cost; }
    }
}
