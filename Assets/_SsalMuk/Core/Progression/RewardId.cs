using System;
namespace SsalMuk.Core
{
    public readonly struct RewardId : IEquatable<RewardId>
    {
        public WeaponKind Weapon { get; }
        public UpgradeKind UpgradeKind { get; }
        public bool IsUpgrade { get; }
        public bool IsSharedUpgrade { get; }
        public SharedUpgradeKind SharedKind { get; }
        private RewardId(WeaponKind weapon, UpgradeKind upgrade, bool isUpgrade, bool shared = false, SharedUpgradeKind sharedKind = default)
        {
            if (!Enum.IsDefined(typeof(WeaponKind), weapon) || !Enum.IsDefined(typeof(UpgradeKind), upgrade)) throw new ArgumentOutOfRangeException(nameof(weapon));
            if (!Enum.IsDefined(typeof(SharedUpgradeKind), sharedKind)) throw new ArgumentOutOfRangeException(nameof(sharedKind));
            Weapon = weapon; UpgradeKind = upgrade; IsUpgrade = isUpgrade; IsSharedUpgrade = shared; SharedKind = sharedKind;
        }
        public static RewardId Acquire(WeaponKind weapon) => new RewardId(weapon, default, false);
        public static RewardId Upgrade(WeaponKind weapon, UpgradeKind upgrade) => new RewardId(weapon, upgrade, true);
        public static RewardId Shared(SharedUpgradeKind kind) => new RewardId(default, default, true, true, kind);
        public bool Equals(RewardId other) => Weapon == other.Weapon && IsUpgrade == other.IsUpgrade && UpgradeKind == other.UpgradeKind && IsSharedUpgrade == other.IsSharedUpgrade && SharedKind == other.SharedKind;
        public override bool Equals(object other) => other is RewardId reward && Equals(reward);
        public override int GetHashCode() => (((int)Weapon * 397 ^ (int)UpgradeKind) * 397 ^ (IsUpgrade ? 1 : 0)) * 397 ^ (IsSharedUpgrade ? 31 + (int)SharedKind : 0);
    }
}
