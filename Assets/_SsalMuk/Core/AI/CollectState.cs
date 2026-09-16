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
            foreach (long id in context.World.Query.QueryExperienceCircle(context.Actor.Position, context.Settings.SearchDistance))
            {
                if (!context.World.TryGetExperience(id, out var xp) || xp.State != ExperienceState.Grounded) continue;
                validIds.Add(id);
                var delta = context.Actor.Position.DisplacementTo(xp.Position);
                double cost = delta.Length * context.Settings.DistanceWeight + context.RiskAt(delta) * context.Settings.RiskWeight;
                candidates.Add(new Candidate { Record = xp, Cost = cost });
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
            foreach (var candidate in candidates)
            {
                if (!TryApproach(context, candidate.Record, out var intent)) continue;
                if (context.CollectionTargetId != candidate.Record.Id) selectedAt = context.Time;
                context.CollectionTargetId = candidate.Record.Id; context.MoveIntent = intent; return;
            }
            context.CollectionTargetId = null;
            // No loot: keep a little room to swing, without chasing an offscreen enemy indefinitely.
            double nearest = double.PositiveInfinity; DVec2 away = DVec2.Zero;
            foreach (var enemy in context.Enemies)
            {
                var delta = context.Actor.Position.DisplacementTo(enemy.Position);
                if (delta.Length < nearest) { nearest = delta.Length; away = -delta.Normalized; }
            }
            if (nearest < 1.5 && context.CanMove(away, 0.5)) context.MoveIntent = away * 0.35;
        }

        private bool TryApproach(AiContext context, ExperienceRecord xp, out DVec2 intent)
        {
            intent = DVec2.Zero; var actor = context.Actor;
            var delta = actor.Position.DisplacementTo(xp.Position);
            if (delta.Length <= context.Settings.PickupApproachRadius * 0.9) return true;
            // Aim just inside the pickup radius. Orbs on walls can be collected from a safe edge.
            var target = xp.Position.Offset(-delta.Normalized * (context.Settings.PickupApproachRadius * 0.85));
            if (!context.World.Query.IsCircleFree(target, actor.BodyRadius)) return false;
            var direct = actor.Position.DisplacementTo(target);
            if (!context.World.Query.SweepCircle(actor.Position, direct, actor.BodyRadius).HasValue)
            { intent = direct.Normalized; return true; }
            if (!routes.TryGetValue(xp.Id, out var route) || route.Revision != context.World.TerrainRevision)
            {
                route?.Request?.Cancel();
                route = new Route { Revision = context.World.TerrainRevision, Request = context.Navigation.RequestPath(actor.Position, target, actor.BodyRadius) };
                routes[xp.Id] = route;
            }
            if (route.Request.Status != PathStatus.Ready) return false;
            while (route.Index < route.Request.Waypoints.Count && actor.Position.DistanceTo(route.Request.Waypoints[route.Index]) < 0.09) route.Index++;
            if (route.Index >= route.Request.Waypoints.Count) { routes.Remove(xp.Id); return false; }
            var step = actor.Position.DisplacementTo(route.Request.Waypoints[route.Index]);
            if (context.World.Query.SweepCircle(actor.Position, step, actor.BodyRadius).HasValue)
            { route.Request.Cancel(); routes.Remove(xp.Id); return false; }
            intent = step.Normalized; return true;
        }
        private sealed class Route { public long Revision; public PathRequest Request; public int Index; }
        private struct Candidate { public ExperienceRecord Record; public double Cost; }
    }
}
