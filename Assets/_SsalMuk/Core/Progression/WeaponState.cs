using System;
using System.Numerics;
namespace SsalMuk.Core
{
    public sealed class WeaponState
    {
        public WeaponKind Kind { get; }
        private readonly BigInteger[] levels = new BigInteger[5];
        public BigInteger GetLevel(UpgradeKind kind)
        {
            if (!Enum.IsDefined(typeof(UpgradeKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            return levels[(int)kind];
        }
        internal void SetLevel(UpgradeKind kind, BigInteger value) => levels[(int)kind] = value;
        internal WeaponState(WeaponKind kind) { Kind = kind; }
    }
}
