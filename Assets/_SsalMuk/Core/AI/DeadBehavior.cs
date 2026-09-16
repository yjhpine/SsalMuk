namespace SsalMuk.Core
{
    public sealed class DeadBehavior : IBehaviorState
    {
        public void Enter(AiContext context) { context.MoveIntent = DVec2.Zero; }
        public void Exit(AiContext context) { }
        public void Tick(AiContext context, double dt) => context.MoveIntent = DVec2.Zero;
    }
}
