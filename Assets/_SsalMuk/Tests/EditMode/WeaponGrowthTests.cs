using System;
using System.Numerics;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class WeaponGrowthTests
    {
        [Test]
        public void UpgradesAffectOnlyTheOwnedWeaponAndSelectedStat()
        {
            using var rig = RunTestRig.Create();
            Assert.Throws<InvalidOperationException>(() => rig.Upgrade(WeaponKind.Spear, UpgradeKind.Damage));
            rig.Equip(WeaponKind.Spear); rig.Upgrade(WeaponKind.Sword, UpgradeKind.Damage);
            rig.Upgrade(WeaponKind.Sword, UpgradeKind.Copies, 2);
            var state = rig.Player.Weapons.Get(WeaponKind.Sword);
            var stats = StatCalculator.Calculate(rig.Run.Definitions.GetWeapon(WeaponKind.Sword), state);
            Assert.That(stats.Damage, Is.EqualTo(8.8).Within(1e-10)); Assert.That(stats.Copies, Is.EqualTo(new BigInteger(5)));
            Assert.That(stats.Repeats, Is.EqualTo(BigInteger.One)); Assert.That(stats.Range, Is.EqualTo(1.6));
            Assert.That(rig.Player.Weapons.Get(WeaponKind.Spear).GetLevel(UpgradeKind.Damage), Is.EqualTo(BigInteger.Zero));
            Assert.That(rig.Run.Definitions.GetWeapon(WeaponKind.Sword).Damage, Is.EqualTo(8));
        }
        [Test]
        public void AllWeaponsApplyRangeSpeedAndRepeatsIndependently()
        {
            using var rig = RunTestRig.Create();
            foreach (WeaponKind kind in Enum.GetValues(typeof(WeaponKind)))
            {
                if (kind != WeaponKind.Sword) rig.Equip(kind);
                rig.Upgrade(kind, UpgradeKind.Range); rig.Upgrade(kind, UpgradeKind.Speed); rig.Upgrade(kind, UpgradeKind.Repeats);
                var definition = rig.Run.Definitions.GetWeapon(kind); var stats = StatCalculator.Calculate(definition, rig.Player.Weapons.Get(kind));
                Assert.That(stats.Range, Is.EqualTo(definition.Range * 1.08).Within(1e-10));
                Assert.That(stats.PeriodSeconds, Is.EqualTo(definition.PeriodSeconds / 1.1).Within(1e-10));
                Assert.That(stats.Repeats, Is.EqualTo(new BigInteger(2))); Assert.That(stats.Copies, Is.EqualTo(BigInteger.One));
            }
        }
        [Test]
        public void HugeCopyCountsStayExactAndUnrepresentableDamageIsRejectedAtomically()
        {
            using var rig = RunTestRig.Create(); BigInteger large = BigInteger.Pow(10, 40);
            rig.Upgrade(WeaponKind.Sword, UpgradeKind.Copies, large);
            var state = rig.Player.Weapons.Get(WeaponKind.Sword);
            Assert.That(StatCalculator.Calculate(rig.Run.Definitions.GetWeapon(WeaponKind.Sword), state).Copies, Is.EqualTo(1 + 2 * large));
            Assert.Throws<NumericRangeException>(() => rig.Upgrade(WeaponKind.Sword, UpgradeKind.Damage, large));
            Assert.That(state.GetLevel(UpgradeKind.Damage), Is.EqualTo(BigInteger.Zero));
        }
    }
}
