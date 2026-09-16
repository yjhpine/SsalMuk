namespace SsalMuk.Core
{
    public interface IBehaviorState
    {
        void Enter(AiContext context);
        void Tick(AiContext context, double dt);
        void Exit(AiContext context);
    }
}
