namespace SsalMuk.Core
{
    public sealed class EvadeState : IBehaviorState
    {
        public void Enter(AiContext context) => context.CollectionTargetId = null;
        public void Exit(AiContext context) { }
        public void Tick(AiContext context, double dt)
        {
            double best = double.PositiveInfinity, bestWall = double.PositiveInfinity; context.MoveIntent = DVec2.Zero;
            double probe = context.Actor.MoveSpeed * context.Settings.EmergencyContactSeconds;
            for (int i = 0; i < context.Settings.DirectionCount; i++)
            {
                var direction = context.Direction(i);
                if (!context.CanMove(direction, probe)) continue;
                double score = context.EscapeRisk(direction);
                double wall = context.LibraryWallCost(direction * context.Settings.ProbeDistance);
                // Wall comfort must never buy a direction with a higher contact/charge risk.
                if (score < best - 1e-9 || System.Math.Abs(score - best) <= 1e-9 && wall < bestWall - 1e-9)
                { best = score; bestWall = wall; context.MoveIntent = direction; }
            }
        }
    }
}
