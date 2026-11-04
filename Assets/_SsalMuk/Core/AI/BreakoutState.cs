using System;

namespace SsalMuk.Core
{
    public sealed class BreakoutState : IBehaviorState
    {
        private WorldPosition progressAnchor;
        private double lastProgress, lastChoice;
        public void Enter(AiContext context) { context.CollectionTargetId = null; Choose(context); }
        public void Exit(AiContext context) => context.BreakoutDirection = DVec2.Zero;
        public void Tick(AiContext context, double dt)
        {
            if (DVec2.Dot(progressAnchor.DisplacementTo(context.Actor.Position), context.BreakoutDirection) > 0.12)
            { progressAnchor = context.Actor.Position; lastProgress = context.Time; }
            bool stalled = context.Time - lastProgress >= context.Settings.NoProgressSeconds;
            double lookAhead = context.World.Map == MapKind.Library ? context.Settings.ProbeDistance : .35;
            // The wall itself can keep blocked-fraction above the release threshold.
            // Reconsider a parallel/outward heading when an interior route may have opened.
            double wall = context.LibraryWallCost(DVec2.Zero);
            bool recoverRoom = wall > 0 && context.Time - lastChoice >= context.Settings.CollectionPlanSeconds &&
                context.LibraryWallCost(context.BreakoutDirection * lookAhead) >= wall - 1e-9;
            if (stalled || recoverRoom || !context.CanMove(context.BreakoutDirection, Math.Max(lookAhead, context.Actor.MoveSpeed * dt))) Choose(context, stalled, recoverRoom && !stalled);
            context.MoveIntent = context.BreakoutDirection;
        }
        private void Choose(AiContext context, bool reconsider = false, bool preserveProgress = false)
        {
            double best = double.PositiveInfinity, bestWall = double.PositiveInfinity; var chosen = DVec2.Zero;
            for (int i = 0; i < context.Settings.DirectionCount; i++)
            {
                var direction = context.Direction(i);
                if (!context.CanMove(direction, context.Settings.ProbeDistance)) continue;
                if (reconsider && DVec2.Dot(direction, context.BreakoutDirection) > 0.95) continue;
                double score = 0;
                foreach (var enemy in context.Enemies)
                {
                    if (!context.InCorridor(enemy, direction, context.Settings.ProbeDistance)) continue;
                    double distance = context.Actor.Position.DistanceTo(enemy.Position);
                    score += 1 + enemy.Health * 0.02 + 1 / Math.Max(0.1, distance);
                }
                // Keep a tied direction on retries, rather than alternate between equivalent exits.
                double continuity = (1 - DVec2.Dot(direction, context.BreakoutDirection)) * 0.01;
                double wall = context.LibraryWallCost(direction * context.Settings.ProbeDistance);
                if (context.World.Map == MapKind.Library) wall += continuity;
                else score += continuity;
                if (score < best - 1e-9 || Math.Abs(score - best) <= 1e-9 && wall < bestWall - 1e-9)
                { best = score; bestWall = wall; chosen = direction; }
            }
            if (chosen == DVec2.Zero && reconsider) { Choose(context); return; }
            if (!preserveProgress || chosen != context.BreakoutDirection)
            { progressAnchor = context.Actor.Position; lastProgress = context.Time; }
            context.BreakoutDirection = chosen; lastChoice = context.Time;
        }
    }
}
