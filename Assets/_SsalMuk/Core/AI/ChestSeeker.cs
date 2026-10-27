using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    // Only the player owns this planner. Emergency states retain its destination, not its movement.
    internal sealed class ChestSeeker
    {
        private readonly ChestSystem chests;
        private readonly List<ChestRecord> candidates = new List<ChestRecord>();
        private readonly Dictionary<long, double> retryAt = new Dictionary<long, double>();
        private PathRequest path;
        private int waypoint;
        private long revision;
        private double nextPlan, progressAt;
        private WorldPosition progressPosition;
        private ExperienceRecord nearbyExperience;
        private double nextExperiencePlan;
        public ChestSeeker(ChestSystem chests) => this.chests = chests;
        public bool TrySteer(AiContext context, double dt, out DVec2 intent)
        {
            intent = DVec2.Zero;
            if (chests.Target != null && chests.Target.Opened) Release();
            if (chests.Target != null && context.Actor.Position.DistanceTo(progressPosition) > .3)
            { progressPosition = context.Actor.Position; progressAt = context.Time; }
            if (chests.Target != null && context.Time - progressAt > 4) Reject(context);
            if (chests.Target == null && context.Time >= nextPlan)
            {
                nextPlan = context.Time + .2; candidates.Clear();
                foreach (var chest in chests.Nearby(context.Actor.Position))
                    if (!chest.Opened && (!retryAt.TryGetValue(chest.Id, out double retry) || retry <= context.Time)) candidates.Add(chest);
                candidates.Sort((a, b) =>
                {
                    int order = context.Actor.Position.DistanceTo(a.Position).CompareTo(context.Actor.Position.DistanceTo(b.Position));
                    return order == 0 ? a.Id.CompareTo(b.Id) : order;
                });
                if (retryAt.Count > 32) retryAt.Clear();
                if (candidates.Count > 0)
                { chests.Target = candidates[0]; progressPosition = context.Actor.Position; progressAt = context.Time; }
            }
            var target = chests.Target; if (target == null) return false;
            var delta = context.Actor.Position.DisplacementTo(target.Position);
            if (delta.Length <= ChestSystem.OpenRadius * .8) return true;
            if (path != null && revision != context.World.TerrainRevision) { path.Cancel(); path = null; }
            if (path == null)
            {
                if (!context.World.Query.SweepCircle(context.Actor.Position, delta, context.Actor.BodyRadius).HasValue)
                { intent = ArrivalIntent(context, delta, dt); return true; }
                path = context.Navigation.RequestPath(context.Actor.Position, target.Position, context.Actor.BodyRadius);
                revision = context.World.TerrainRevision; waypoint = 0;
            }
            if (path.Status == PathStatus.NoPath) { Reject(context); return false; }
            if (path.Status == PathStatus.Pending) return true;
            while (waypoint < path.Waypoints.Count && context.Actor.Position.DistanceTo(path.Waypoints[waypoint]) < .15) waypoint++;
            if (waypoint == path.Waypoints.Count) { path = null; return true; }
            var step = context.Actor.Position.DisplacementTo(path.Waypoints[waypoint]);
            if (context.World.Query.SweepCircle(context.Actor.Position, step, context.Actor.BodyRadius).HasValue)
            { path.Cancel(); path = null; return false; }
            intent = ArrivalIntent(context, step, dt); return true;
        }
        public void Interrupt(AiContext context)
        {
            progressAt = context.Time; progressPosition = context.Actor.Position;
            path?.Cancel(); path = null;
        }
        public DVec2 GatherAlongRoute(AiContext context, DVec2 routeIntent, double dt)
        {
            context.CollectionTargetId = null;
            if (routeIntent.Length < .1) return routeIntent;
            // A short, clear detour is allowed; the long-term destination remains the chest.
            if (context.Time >= nextExperiencePlan)
            {
                nextExperiencePlan = context.Time + .2; nearbyExperience = null;
                double best = double.PositiveInfinity;
                foreach (long id in context.World.Query.QueryExperienceCircle(context.Actor.Position, Math.Min(6, context.PickupRadius + 2)))
                {
                    if (!context.World.TryGetExperience(id, out var xp) || xp.State != ExperienceState.Grounded ||
                        !TryPickupApproach(context, xp, routeIntent, out var approach)) continue;
                    if (approach.Length < best) { best = approach.Length; nearbyExperience = xp; }
                }
            }
            if (nearbyExperience != null && context.World.TryGetExperience(nearbyExperience.Id, out var current) &&
                current.State == ExperienceState.Grounded && TryPickupApproach(context, current, routeIntent, out var step))
            { context.CollectionTargetId = current.Id; return ArrivalIntent(context, step, dt); }
            return routeIntent;
        }
        private static DVec2 ArrivalIntent(AiContext context, DVec2 delta, double dt)
            => delta / Math.Max(delta.Length, context.Actor.MoveSpeed * dt);
        private static bool TryPickupApproach(AiContext context, ExperienceRecord xp, DVec2 routeIntent, out DVec2 approach)
        {
            var delta = context.Actor.Position.DisplacementTo(xp.Position);
            approach = delta.Normalized * Math.Max(0, delta.Length - context.PickupRadius * .8);
            return delta.Length > context.PickupRadius * .9 && delta.Length <= Math.Min(6, context.PickupRadius + 2) &&
                DVec2.Dot(approach, routeIntent) > .05 &&
                !context.World.Query.SweepCircle(context.Actor.Position, approach, context.Actor.BodyRadius).HasValue;
        }
        private void Reject(AiContext context)
        { retryAt[chests.Target.Id] = context.Time + 5; Release(); nextPlan = context.Time + .2; }
        private void Release() { path?.Cancel(); path = null; chests.Target = null; }
    }
}
