using System;
using System.Numerics;
namespace SsalMuk.Core
{
    public sealed class WeaponState
    {
        public const int MaximumRepeatLevel = 2;
        public WeaponKind Kind { get; }
        private readonly BigInteger[] levels = new BigInteger[5];
        public BigInteger GetLevel(UpgradeKind kind)
        {
            if (!Enum.IsDefined(typeof(UpgradeKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            return levels[(int)kind];
        }
        public bool CanUpgrade(UpgradeKind kind) => kind != UpgradeKind.Repeats || GetLevel(kind) < MaximumRepeatLevel;
        internal void SetLevel(UpgradeKind kind, BigInteger value)
        {
            if (value < 0 || (kind == UpgradeKind.Repeats && value > MaximumRepeatLevel)) throw new ArgumentOutOfRangeException(nameof(value));
            levels[(int)kind] = value;
        }
        internal WeaponState(WeaponKind kind) { Kind = kind; }
    }
}
