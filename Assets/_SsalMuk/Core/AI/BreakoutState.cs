using System;

namespace SsalMuk.Core
{
    public sealed class BreakoutState : IBehaviorState
    {
        private WorldPosition progressAnchor;
        private double lastProgress;
        public void Enter(AiContext context) { context.CollectionTargetId = null; Choose(context); }
        public void Exit(AiContext context) => context.BreakoutDirection = DVec2.Zero;
        public void Tick(AiContext context, double dt)
        {
            if (DVec2.Dot(progressAnchor.DisplacementTo(context.Actor.Position), context.BreakoutDirection) > 0.12)
            { progressAnchor = context.Actor.Position; lastProgress = context.Time; }
            bool stalled = context.Time - lastProgress >= context.Settings.NoProgressSeconds;
            if (stalled || !context.CanMove(context.BreakoutDirection, Math.Max(0.35, context.Actor.Definition.MoveSpeed * dt))) Choose(context, stalled);
            context.MoveIntent = context.BreakoutDirection;
        }
        private void Choose(AiContext context, bool reconsider = false)
        {
            double best = double.PositiveInfinity; var chosen = DVec2.Zero;
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
                score += (1 - DVec2.Dot(direction, context.BreakoutDirection)) * 0.01;
                if (score < best - 1e-9) { best = score; chosen = direction; }
            }
            if (chosen == DVec2.Zero && reconsider) { Choose(context); return; }
            context.BreakoutDirection = chosen; progressAnchor = context.Actor.Position; lastProgress = context.Time;
        }
    }
}
