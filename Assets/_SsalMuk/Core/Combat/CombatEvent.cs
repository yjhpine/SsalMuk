namespace SsalMuk.Core
{
    public readonly struct CombatEvent
    {
        public DamageRequest Request { get; }
        public double Time { get; }
        public double RemainingHealth { get; }
        public bool Killed => RemainingHealth <= 0;
        public CombatEvent(DamageRequest request, double time, double remainingHealth)
        { Request = request; Time = time; RemainingHealth = remainingHealth; }
    }
}
