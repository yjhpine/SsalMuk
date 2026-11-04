namespace SsalMuk.Core
{
    public enum DamageOrigin { Weapon, Item, Contact }
    public readonly struct DamageRequest
    {
        public HitKey Key { get; }
        public long SourceId { get; }
        public long TargetId { get; }
        public double Amount { get; }
        public DVec2 Direction { get; }
        public double KnockbackDistance { get; }
        public double KnockbackSeconds { get; }
        public DamageOrigin Origin { get; }
        public DamageRequest(HitKey key, long sourceId, long targetId, double amount, DVec2 direction,
            double knockbackDistance = 0.3, double knockbackSeconds = 0.15, DamageOrigin origin = DamageOrigin.Weapon)
        { Key = key; SourceId = sourceId; TargetId = targetId; Amount = amount; Direction = direction;
            KnockbackDistance = knockbackDistance; KnockbackSeconds = knockbackSeconds; Origin = origin; }
    }
}
