namespace SsalMuk.Core
{
    public sealed class AirFlyBehavior : IBehaviorState
    {
        public void Enter(AiContext context) { }
        public void Exit(AiContext context) { }
        public void Tick(AiContext context, double dt) => context.MoveIntent = ((AirEnemyModel)context.Actor).OriginalDirection;
    }
}
