namespace SsalMuk.Core
{
    public readonly struct KnockbackState
    {
        public DVec2 Velocity { get; }
        public double RemainingSeconds { get; }
        public bool IsActive => RemainingSeconds > 0;
        public KnockbackState(DVec2 velocity, double remainingSeconds)
        { Velocity = velocity; RemainingSeconds = remainingSeconds; }
    }
}
