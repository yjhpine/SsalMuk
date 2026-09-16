namespace SsalMuk.Core
{
    public sealed class AttackInstance
    {
        public HitKey Key { get; }
        public WeaponKind Kind { get; }
        public double StartedAt { get; }
        public double ActiveSeconds { get; }
        public DVec2 Direction { get; }
        public WeaponStats Stats { get; }
        public WorldPosition Origin { get; internal set; }
        public double Progress { get; internal set; }
        public AttackInstance(HitKey key, WeaponKind kind, double startedAt, double activeSeconds, DVec2 direction, WeaponStats stats, WorldPosition origin)
        { Key = key; Kind = kind; StartedAt = startedAt; ActiveSeconds = activeSeconds; Direction = direction; Stats = stats; Origin = origin; }
    }
}
