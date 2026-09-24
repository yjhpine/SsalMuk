using System;
using System.Numerics;
namespace SsalMuk.Core
{
    public sealed class WeaponState
    {
        public const int MaximumRepeatLevel = 2;
        public static int MaximumCopies(WeaponKind kind)
        {
            switch (kind)
            {
                case WeaponKind.Sword: case WeaponKind.Spear: case WeaponKind.Axe: return 8;
                case WeaponKind.Fireball: return 6;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
        public WeaponKind Kind { get; }
        private readonly BigInteger[] levels = new BigInteger[5];
        public BigInteger GetLevel(UpgradeKind kind)
        {
            if (!Enum.IsDefined(typeof(UpgradeKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            return levels[(int)kind];
        }
        public bool CanUpgrade(UpgradeKind kind)
        {
            BigInteger level = GetLevel(kind);
            return kind == UpgradeKind.Copies ? level < MaximumCopies(Kind) - 1 :
                kind != UpgradeKind.Repeats || level < MaximumRepeatLevel;
        }
        internal void SetLevel(UpgradeKind kind, BigInteger value)
        {
            GetLevel(kind);
            if (value < 0 || (kind == UpgradeKind.Repeats && value > MaximumRepeatLevel) ||
                (kind == UpgradeKind.Copies && value >= MaximumCopies(Kind))) throw new ArgumentOutOfRangeException(nameof(value));
            levels[(int)kind] = value;
        }
        internal WeaponState(WeaponKind kind) { Kind = kind; }
    }
}
