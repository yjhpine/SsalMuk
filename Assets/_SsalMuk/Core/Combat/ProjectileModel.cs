namespace SsalMuk.Core
{
    public sealed class ProjectileModel
    {
        public HitKey Key { get; }
        public WorldPosition Position { get; internal set; }
        public WorldPosition PreviousPosition { get; internal set; }
        public DVec2 Direction { get; }
        public DVec2 Velocity { get; }
        public WeaponStats Stats { get; }
        public double SpawnedAt { get; }
        public double ExpiresAt { get; }
        public double Radius { get; }
        internal double UpdatedAt { get; set; }
        public bool IsAlive { get; internal set; } = true;
        public long? HitTargetId { get; internal set; }
        internal ProjectileModel(AttackInstance attack, WeaponDefinition definition)
        {
            Key = attack.Key; Position = PreviousPosition = attack.Origin; Direction = attack.Direction.Normalized;
            Velocity = Direction * definition.ProjectileSpeed; Stats = attack.Stats;
            SpawnedAt = attack.StartedAt; ExpiresAt = SpawnedAt + definition.ProjectileLifetime; Radius = definition.Width;
            UpdatedAt = SpawnedAt;
            if (!(ExpiresAt > SpawnedAt) || double.IsInfinity(ExpiresAt)) throw new NumericRangeException("Projectile lifetime is not representable on this clock.");
        }
    }
}
