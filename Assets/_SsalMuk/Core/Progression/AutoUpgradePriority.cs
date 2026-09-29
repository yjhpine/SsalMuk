using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    public static class AutoUpgradePriority
    {
        public static int SelectSlot(IReadOnlyList<RewardId> choices, WeaponKind startingWeapon)
        {
            if (choices == null || choices.Count == 0) throw new ArgumentException("At least one offered reward is required.", nameof(choices));
            int best = 0, rank = Rank(choices[0], startingWeapon);
            for (int i = 1; i < choices.Count; i++)
            {
                int candidate = Rank(choices[i], startingWeapon);
                if (candidate < rank) { best = i; rank = candidate; }
            }
            return best;
        }

        private static int Rank(RewardId reward, WeaponKind startingWeapon)
        {
            if (reward.IsSharedUpgrade)
            {
                switch (reward.SharedKind)
                {
                    case SharedUpgradeKind.Experience: return 0;
                    case SharedUpgradeKind.MoveSpeed: return 6;
                    case SharedUpgradeKind.Regeneration: return 7;
                    case SharedUpgradeKind.PickupRange: return 8;
                    default: throw new ArgumentOutOfRangeException(nameof(reward));
                }
            }
            if (!reward.IsUpgrade) return 14;
            int group = reward.Weapon == startingWeapon ? 1 : 9;
            switch (reward.UpgradeKind)
            {
                case UpgradeKind.Repeats: return group;
                case UpgradeKind.Copies: return group + 1;
                case UpgradeKind.Damage: return group + 2;
                case UpgradeKind.Speed: return group + 3;
                case UpgradeKind.Range: return group + 4;
                default: throw new ArgumentOutOfRangeException(nameof(reward));
            }
        }
    }
}
