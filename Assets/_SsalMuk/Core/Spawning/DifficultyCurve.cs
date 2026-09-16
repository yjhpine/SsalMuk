using System;
namespace SsalMuk.Core
{
    public sealed class DifficultyCurve
    {
        private readonly SpawnSettings settings;
        public DifficultyCurve(SpawnSettings settings) { this.settings = settings ?? throw new ArgumentNullException(nameof(settings)); }
        public double NormalDeadline(long ordinal)
        {
            if (ordinal <= 0) throw new ArgumentOutOfRangeException(nameof(ordinal));
            double rate = settings.NormalBaseRate, slope = settings.NormalRateGrowth;
            // Invert integral(rate + slope*t), without subtracting nearly equal large values.
            double time = slope == 0 ? ordinal / rate : 2.0 * ordinal / (rate + Math.Sqrt(rate * rate + 2 * slope * ordinal));
            if (!(time > 0) || double.IsInfinity(time)) throw new NumericRangeException("Normal spawn deadline is not representable.");
            return time;
        }
        public long AirCount(double time)
        {
            RequireTime(time); double increase = Math.Floor(time / settings.AirCountGrowthSeconds);
            if (increase >= long.MaxValue - (double)settings.AirBaseCount) throw new NumericRangeException("Air wave count exceeds its integer representation.");
            return checked(settings.AirBaseCount + (long)increase);
        }
        public UnitDefinition AtSpawn(UnitDefinition baseline, double time)
        {
            if (baseline == null) throw new ArgumentNullException(nameof(baseline)); RequireTime(time);
            if (baseline.Kind == UnitKind.Player) return baseline;
            double scale = 1 + time / 300, health = baseline.MaxHealth * scale, damage = baseline.ContactDamage * scale;
            if (double.IsInfinity(health) || double.IsInfinity(damage)) throw new NumericRangeException("Spawn difficulty exceeds numeric range.");
            return new UnitDefinition(baseline.Id, baseline.Kind, health, baseline.MoveSpeed, baseline.BodyRadius, damage, baseline.ExperienceReward);
        }
        private static void RequireTime(double time)
        { if (time < 0 || double.IsNaN(time) || double.IsInfinity(time)) throw new ArgumentOutOfRangeException(nameof(time)); }
    }
}
