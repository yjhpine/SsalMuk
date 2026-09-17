using System;
namespace SsalMuk.Core
{
    public sealed class SurroundWaveSettings
    {
        public const double Interval = 120;
        public int BaseCount { get; }
        public int CountStep { get; }
        public double HealthStep { get; }
        public double DamageStep { get; }
        public double FastClearSeconds { get; }
        public double FastKillFraction { get; }
        public double RemainingFraction { get; }
        public double ReservationSeconds { get; }
        public SurroundWaveSettings(int baseCount = 80, int countStep = 20, double healthStep = .2, double damageStep = .1,
            double fastClearSeconds = 45, double fastKillFraction = .8, double remainingFraction = .25, double reservationSeconds = 5)
        {
            if (baseCount < 0 || countStep <= 0) throw new ArgumentOutOfRangeException(nameof(baseCount));
            foreach (double value in new[] { healthStep, damageStep, fastClearSeconds, reservationSeconds }) WeaponDefinition.RequirePositive(value, nameof(value));
            if (!(fastKillFraction > 0 && fastKillFraction <= 1) || !(remainingFraction >= 0 && remainingFraction <= 1) ||
                fastClearSeconds >= Interval || reservationSeconds >= fastClearSeconds) throw new ArgumentOutOfRangeException(nameof(fastKillFraction));
            BaseCount = baseCount; CountStep = countStep; HealthStep = healthStep; DamageStep = damageStep;
            FastClearSeconds = fastClearSeconds; FastKillFraction = fastKillFraction; RemainingFraction = remainingFraction; ReservationSeconds = reservationSeconds;
        }
    }
}
