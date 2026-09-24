using System;

namespace SsalMuk.Core
{
    public enum PowerupKind { Magnet, Invincibility, MoveSpeed, AttackRange }

    public sealed class PowerupRecord
    {
        public long Id { get; }
        public PowerupKind Kind { get; }
        public WorldPosition Position { get; }
        internal PowerupRecord(long id, PowerupKind kind, WorldPosition position)
        { Id = id; Kind = kind; Position = position; }
    }

    public sealed class PowerupSettings
    {
        public double DropChance { get; }
        public const double Duration = 10, SpeedMultiplier = 1.5, RangeMultiplier = 3;
        public PowerupSettings(double dropChance = .02)
        {
            if (double.IsNaN(dropChance) || dropChance < 0 || dropChance > 1) throw new ArgumentOutOfRangeException(nameof(dropChance));
            DropChance = dropChance;
        }
    }

    public sealed class PlayerEffects
    {
        public double Time { get; private set; }
        public double InvincibilityUntil { get; private set; }
        public double SpeedUntil { get; private set; }
        public double RangeUntil { get; private set; }
        public double SpeedMultiplier => Time < SpeedUntil ? PowerupSettings.SpeedMultiplier : 1;
        public double RangeMultiplier => Time < RangeUntil ? PowerupSettings.RangeMultiplier : 1;
        internal void Advance(double now) => Time = now;
        internal void Refresh(PowerupKind kind, double now)
        {
            Time = now;
            if (kind == PowerupKind.Invincibility) InvincibilityUntil = now + PowerupSettings.Duration;
            else if (kind == PowerupKind.MoveSpeed) SpeedUntil = now + PowerupSettings.Duration;
            else if (kind == PowerupKind.AttackRange) RangeUntil = now + PowerupSettings.Duration;
        }
    }
}
