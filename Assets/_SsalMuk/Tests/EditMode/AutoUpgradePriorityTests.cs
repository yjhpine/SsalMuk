using System.Collections.Generic;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class AutoUpgradePriorityTests
    {
        [TestCase(WeaponKind.Sword), TestCase(WeaponKind.Spear), TestCase(WeaponKind.Axe), TestCase(WeaponKind.Fireball)]
        public void EveryPriorityBoundaryUsesTheStartingWeaponAndNotOfferPosition(WeaponKind start)
        {
            var other = start == WeaponKind.Sword ? WeaponKind.Spear : WeaponKind.Sword;
            var order = new List<RewardId> { RewardId.Shared(SharedUpgradeKind.Experience) };
            var upgrades = new[] { UpgradeKind.Repeats, UpgradeKind.Copies, UpgradeKind.Damage, UpgradeKind.Speed, UpgradeKind.Range };
            foreach (var upgrade in upgrades) order.Add(RewardId.Upgrade(start, upgrade));
            order.Add(RewardId.Shared(SharedUpgradeKind.MoveSpeed));
            order.Add(RewardId.Shared(SharedUpgradeKind.Regeneration));
            order.Add(RewardId.Shared(SharedUpgradeKind.PickupRange));
            foreach (var upgrade in upgrades) order.Add(RewardId.Upgrade(other, upgrade));
            order.Add(RewardId.Acquire(other));
            for (int i = 1; i < order.Count; i++)
            {
                Assert.That(AutoUpgradePriority.SelectSlot(new[] { order[i], order[i - 1] }, start), Is.EqualTo(1), "Boundary " + i);
                Assert.That(AutoUpgradePriority.SelectSlot(new[] { order[i - 1], order[i] }, start), Is.EqualTo(0), "Boundary " + i);
            }
        }

        [Test]
        public void EqualPriorityUsesTheLeftmostOfferedCard()
        {
            Assert.That(AutoUpgradePriority.SelectSlot(new[] { RewardId.Acquire(WeaponKind.Fireball), RewardId.Acquire(WeaponKind.Spear),
                RewardId.Acquire(WeaponKind.Axe) }, WeaponKind.Sword), Is.Zero);
            Assert.That(AutoUpgradePriority.SelectSlot(new[] { RewardId.Upgrade(WeaponKind.Axe, UpgradeKind.Damage),
                RewardId.Upgrade(WeaponKind.Spear, UpgradeKind.Damage), RewardId.Acquire(WeaponKind.Fireball) }, WeaponKind.Sword), Is.Zero);
        }
    }
}
