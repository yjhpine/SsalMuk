using System;
namespace SsalMuk.Core
{
    public readonly struct RewardId : IEquatable<RewardId>
    {
        public WeaponKind Weapon { get; }
        public UpgradeKind UpgradeKind { get; }
        public bool IsUpgrade { get; }
        private RewardId(WeaponKind weapon, UpgradeKind upgrade, bool isUpgrade)
        {
            if (!Enum.IsDefined(typeof(WeaponKind), weapon) || !Enum.IsDefined(typeof(UpgradeKind), upgrade)) throw new ArgumentOutOfRangeException(nameof(weapon));
            Weapon = weapon; UpgradeKind = upgrade; IsUpgrade = isUpgrade;
        }
        public static RewardId Acquire(WeaponKind weapon) => new RewardId(weapon, default, false);
        public static RewardId Upgrade(WeaponKind weapon, UpgradeKind upgrade) => new RewardId(weapon, upgrade, true);
        public bool Equals(RewardId other) => Weapon == other.Weapon && IsUpgrade == other.IsUpgrade && UpgradeKind == other.UpgradeKind;
        public override bool Equals(object other) => other is RewardId reward && Equals(reward);
        public override int GetHashCode() => ((int)Weapon * 397 ^ (int)UpgradeKind) * 397 ^ (IsUpgrade ? 1 : 0);
    }
}
