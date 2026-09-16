using System;
using System.Numerics;

namespace SsalMuk.Core
{
    public sealed class GrowthSettings
    {
        public BigInteger FirstCost { get; }
        public BigInteger CostStep { get; }
        public double DamageCoefficient { get; }
        public double SpeedCoefficient { get; }
        public double RangeCoefficient { get; }
        public GrowthSettings(BigInteger? firstCost = null, BigInteger? costStep = null,
            double damageCoefficient = 0.1, double speedCoefficient = 0.1, double rangeCoefficient = 0.08)
        {
            FirstCost = firstCost ?? 5; CostStep = costStep ?? 3;
            if (FirstCost <= 0 || CostStep <= 0) throw new ArgumentOutOfRangeException(nameof(firstCost), "Level costs must be positive.");
            WeaponDefinition.RequirePositive(damageCoefficient, nameof(damageCoefficient));
            WeaponDefinition.RequirePositive(speedCoefficient, nameof(speedCoefficient));
            WeaponDefinition.RequirePositive(rangeCoefficient, nameof(rangeCoefficient));
            DamageCoefficient = damageCoefficient; SpeedCoefficient = speedCoefficient; RangeCoefficient = rangeCoefficient;
        }
        public BigInteger CostForLevels(BigInteger level, BigInteger count)
        {
            if (level <= 0 || count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            return count * (FirstCost + CostStep * (level - 1)) + CostStep * count * (count - 1) / 2;
        }
    }
}
