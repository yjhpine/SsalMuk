namespace SsalMuk.Core
{
    public sealed class KnockedBackBehavior : IBehaviorState
    {
        public void Enter(AiContext context) { }
        public void Exit(AiContext context) { }
        public void Tick(AiContext context, double dt) => context.MoveIntent = DVec2.Zero;
    }
}
