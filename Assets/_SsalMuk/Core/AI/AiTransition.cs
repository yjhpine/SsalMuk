namespace SsalMuk.Core
{
    public readonly struct AiTransition
    {
        public double Time { get; }
        public BrainState Previous { get; }
        public BrainState Next { get; }
        public string Reason { get; }
        public AiTransition(double time, BrainState previous, BrainState next, string reason)
        { Time = time; Previous = previous; Next = next; Reason = reason; }
    }
}
