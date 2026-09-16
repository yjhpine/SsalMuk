using System;
using System.Numerics;

namespace SsalMuk.Core
{
    public sealed class WeaponStats
    {
        public double Damage { get; }
        public double Range { get; }
        public double PeriodSeconds { get; }
        public BigInteger Copies { get; }
        public BigInteger Repeats { get; }
        public double BurstFraction { get; }
        public WeaponStats(double damage, double range, double periodSeconds, BigInteger? copies = null,
            BigInteger? repeats = null, double burstFraction = 0.6)
        {
            WeaponDefinition.RequirePositive(damage, nameof(damage)); WeaponDefinition.RequirePositive(range, nameof(range));
            WeaponDefinition.RequirePositive(periodSeconds, nameof(periodSeconds));
            if (copies.HasValue && copies.Value <= 0) throw new ArgumentOutOfRangeException(nameof(copies));
            if (repeats.HasValue && repeats.Value <= 0) throw new ArgumentOutOfRangeException(nameof(repeats));
            if (!(burstFraction > 0 && burstFraction <= 1)) throw new ArgumentOutOfRangeException(nameof(burstFraction));
            Damage = damage; Range = range; PeriodSeconds = periodSeconds; Copies = copies ?? BigInteger.One; Repeats = repeats ?? BigInteger.One; BurstFraction = burstFraction;
        }
    }
}
