namespace SsalMuk.Core
{
    public sealed class EvadeState : IBehaviorState
    {
        public void Enter(AiContext context) => context.CollectionTargetId = null;
        public void Exit(AiContext context) { }
        public void Tick(AiContext context, double dt)
        {
            double best = double.PositiveInfinity, bestDanger = double.PositiveInfinity; context.MoveIntent = DVec2.Zero;
            double probe = context.Actor.MoveSpeed * context.Settings.EmergencyContactSeconds;
            for (int i = 0; i < context.Settings.DirectionCount; i++)
            {
                var direction = context.Direction(i);
                if (!context.CanMove(direction, probe)) continue;
                double score = context.EscapeRisk(direction, out double danger);
                if (context.World.Map == MapKind.Library)
                    score += context.LibraryWallCost(direction * context.Settings.ProbeDistance);
                else danger = 0; // Preserve the existing forest escape ranking.
                // Immediate collisions/charge lanes win first; safe choices also preserve turning room.
                if (danger < bestDanger - 1e-9 || System.Math.Abs(danger - bestDanger) <= 1e-9 && score < best - 1e-9)
                { best = score; bestDanger = danger; context.MoveIntent = direction; }
            }
        }
    }
}
