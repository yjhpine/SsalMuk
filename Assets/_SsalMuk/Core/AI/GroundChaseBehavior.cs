namespace SsalMuk.Core
{
    public sealed class GroundChaseBehavior : IBehaviorState
    {
        public void Enter(AiContext context) { }
        public void Exit(AiContext context) { }
        public void Tick(AiContext context, double dt) => context.MoveIntent = context.FollowTarget != null && context.FollowTarget.IsAlive ?
            context.Navigation.ChaseDirection(context.Actor, context.FollowTarget.Position) : DVec2.Zero;
    }
}
