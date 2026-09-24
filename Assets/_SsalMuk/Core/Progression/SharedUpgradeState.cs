using System;
using System.Numerics;

namespace SsalMuk.Core
{
    public enum SharedUpgradeKind { MoveSpeed, Regeneration, PickupRange, Experience }

    public sealed class SharedUpgradeState
    {
        private readonly BigInteger[] levels = new BigInteger[4];
        public BigInteger GetLevel(SharedUpgradeKind kind) => levels[Index(kind)];
        public bool CanUpgrade(SharedUpgradeKind kind)
        {
            var level = GetLevel(kind);
            if (kind == SharedUpgradeKind.Experience) return true;
            double coefficient = kind == SharedUpgradeKind.PickupRange ? .2 : .1;
            double baseline = kind == SharedUpgradeKind.Regeneration ? 0 : 1;
            double next = baseline + coefficient * (double)(level + 1);
            return !double.IsInfinity(next) && next > baseline + coefficient * (double)level;
        }
        public double SpeedMultiplier => Factor(SharedUpgradeKind.MoveSpeed, .1, 1);
        public double HealingPerSecond => Factor(SharedUpgradeKind.Regeneration, .1, 0);
        public double PickupMultiplier => Factor(SharedUpgradeKind.PickupRange, .2, 1);
        public BigInteger ExperienceHundredthsFor(BigInteger raw)
        {
            if (raw <= 0) throw new ArgumentOutOfRangeException(nameof(raw));
            // Exact hundredths preserve every 15% bonus, including values beyond floating-point precision.
            return raw * (100 + 15 * GetLevel(SharedUpgradeKind.Experience));
        }
        internal void Upgrade(SharedUpgradeKind kind, BigInteger count)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            int index = Index(kind); var next = levels[index] + count;
            if (kind != SharedUpgradeKind.Experience)
            {
                double coefficient = kind == SharedUpgradeKind.PickupRange ? .2 : .1;
                double baseline = kind == SharedUpgradeKind.Regeneration ? 0 : 1;
                double value = baseline + coefficient * (double)next;
                double previous = baseline + coefficient * (double)(next - 1);
                if (double.IsInfinity(value) || double.IsNaN(value) || value <= previous)
                    throw new NumericRangeException("The shared upgrade exceeds numeric precision.");
            }
            levels[index] = next;
        }
        private double Factor(SharedUpgradeKind kind, double coefficient, double baseline)
            => baseline + coefficient * (double)GetLevel(kind);
        private static int Index(SharedUpgradeKind kind)
        {
            if (!Enum.IsDefined(typeof(SharedUpgradeKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            return (int)kind;
        }
    }
}
