namespace SsalMuk.Core
{
    public readonly struct CombatEvent
    {
        public DamageRequest Request { get; }
        public double Time { get; }
        public double RemainingHealth { get; }
        public double ActualHealthDamage { get; }
        public bool Killed => RemainingHealth <= 0;
        public CombatEvent(DamageRequest request, double time, double remainingHealth, double? actualHealthDamage = null)
        { Request = request; Time = time; RemainingHealth = remainingHealth; ActualHealthDamage = actualHealthDamage ?? request.Amount; }
    }
}
