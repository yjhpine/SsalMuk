using System;
using System.Numerics;

namespace SsalMuk.Core
{
    public static class StatCalculator
    {
        public static WeaponStats Calculate(WeaponDefinition definition, WeaponState state, GrowthSettings settings = null)
            => Calculate(definition, state, settings ?? new GrowthSettings(), null, BigInteger.Zero);
        internal static WeaponStats Calculate(WeaponDefinition definition, WeaponState state, GrowthSettings settings, UpgradeKind? replacement, BigInteger next)
        {
            if (definition == null || state == null) throw new ArgumentNullException(nameof(state));
            if (definition.Kind != state.Kind) throw new ArgumentException("The weapon state and definition must match.");
            BigInteger Level(UpgradeKind kind) => replacement == kind ? next : state.GetLevel(kind);
            return new WeaponStats(Scaled(definition.Damage, Level(UpgradeKind.Damage), settings.DamageCoefficient, false, "Damage"),
                Scaled(definition.Range, Level(UpgradeKind.Range), settings.RangeCoefficient, false, "Range"),
                Scaled(definition.PeriodSeconds, Level(UpgradeKind.Speed), settings.SpeedCoefficient, true, "Period"),
                1 + Level(UpgradeKind.Copies), 1 + Level(UpgradeKind.Repeats));
        }
        private static double Scaled(double baseline, BigInteger level, double coefficient, bool inverse, string name)
        {
            if (level.IsZero) return baseline;
            double factor = 1 + coefficient * (double)level;
            double previousFactor = 1 + coefficient * (double)(level - 1);
            double value = inverse ? baseline / factor : baseline * factor;
            double previous = inverse ? baseline / previousFactor : baseline * previousFactor;
            if (!(value > 0) || double.IsInfinity(value) || double.IsNaN(value) || (inverse ? value >= previous : value <= previous))
                throw new NumericRangeException(name + " cannot represent the requested upgrade and its next-unit precision.");
            return value;
        }
    }
}
