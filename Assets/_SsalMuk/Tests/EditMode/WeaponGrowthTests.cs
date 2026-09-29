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
            Assert.That(stats.Damage, Is.EqualTo(8.8).Within(1e-10)); Assert.That(stats.Copies, Is.EqualTo(new BigInteger(3)));
            Assert.That(stats.Repeats, Is.EqualTo(BigInteger.One)); Assert.That(stats.Range, Is.EqualTo(1.6));
            Assert.That(rig.Player.Weapons.Get(WeaponKind.Spear).GetLevel(UpgradeKind.Damage), Is.EqualTo(BigInteger.Zero));
            Assert.That(rig.Run.Definitions.GetWeapon(WeaponKind.Sword).Damage, Is.EqualTo(8));
        }
        [Test]
        public void AllWeaponsApplyRangeAndSpeedWithoutAddingRepeatAttacks()
        {
            using var rig = RunTestRig.Create();
            foreach (WeaponKind kind in Enum.GetValues(typeof(WeaponKind)))
            {
                if (kind != WeaponKind.Sword) rig.Equip(kind);
                rig.Upgrade(kind, UpgradeKind.Range); rig.Upgrade(kind, UpgradeKind.Speed);
                var definition = rig.Run.Definitions.GetWeapon(kind); var stats = StatCalculator.Calculate(definition, rig.Player.Weapons.Get(kind));
                Assert.That(stats.Range, Is.EqualTo(definition.Range * 1.04).Within(1e-10));
                Assert.That(stats.PeriodSeconds, Is.EqualTo(definition.PeriodSeconds / 1.15).Within(1e-10));
                Assert.That(stats.Repeats, Is.EqualTo(BigInteger.One)); Assert.That(stats.Copies, Is.EqualTo(BigInteger.One));
            }
        }
        [TestCase(0, 1)] [TestCase(1, 1.15)] [TestCase(9, 2.35)] [TestCase(10, 2.5)]
        [TestCase(11, 2.55)] [TestCase(15, 2.75)] [TestCase(30, 3.5)] [TestCase(100, 7)]
        public void SpeedUsesFifteenPercentForTenUpgradesThenFivePercent(int level, double multiplier)
        {
            using var rig = RunTestRig.Create(enableAi: false);
            foreach (WeaponKind kind in Enum.GetValues(typeof(WeaponKind)))
            {
                if (kind != WeaponKind.Sword) rig.Equip(kind);
                if (level > 0) rig.Upgrade(kind, UpgradeKind.Speed, level);
                var definition = rig.Run.Definitions.GetWeapon(kind);
                var stats = StatCalculator.Calculate(definition, rig.Player.Weapons.Get(kind), rig.Run.GrowthSettings);
                Assert.That(stats.PeriodSeconds, Is.EqualTo(definition.PeriodSeconds / multiplier).Within(1e-12), kind.ToString());
                Assert.That(stats.Damage, Is.EqualTo(definition.Damage));
                Assert.That(stats.Range, Is.EqualTo(definition.Range));
            }
        }
        [Test]
        public void RepeatUpgradeIsRejectedWithoutChangingTheWeapon()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            var state = rig.Player.Weapons.Get(WeaponKind.Sword);
            Assert.Throws<InvalidOperationException>(() => rig.Upgrade(WeaponKind.Sword, UpgradeKind.Repeats));
            Assert.That(state.GetLevel(UpgradeKind.Repeats), Is.EqualTo(BigInteger.Zero));
            Assert.That(state.CanUpgrade(UpgradeKind.Repeats), Is.False);
        }
        [Test]
        public void CrossingSpeedBoundaryInOneUpgradeMatchesIncrementalGrowth()
        {
            using var bulk = RunTestRig.Create(enableAi: false);
            using var incremental = RunTestRig.Create(enableAi: false);
            bulk.Upgrade(WeaponKind.Sword, UpgradeKind.Speed, 9);
            bulk.Upgrade(WeaponKind.Sword, UpgradeKind.Speed, 2);
            for (int i = 0; i < 11; i++) incremental.Upgrade(WeaponKind.Sword, UpgradeKind.Speed);
            double Period(RunTestRig rig) => StatCalculator.Calculate(rig.Run.Definitions.GetWeapon(WeaponKind.Sword),
                rig.Player.Weapons.Get(WeaponKind.Sword), rig.Run.GrowthSettings).PeriodSeconds;
            Assert.That(Period(bulk), Is.EqualTo(1 / 2.55).Within(1e-12));
            Assert.That(Period(bulk), Is.EqualTo(Period(incremental)));
        }
        [Test]
        public void ActualCatalogUsesTheNewGrowthCurveAndKeepsLevelCosts()
        {
            var catalog = UnityEngine.Resources.Load<SsalMuk.Unity.GameCatalog>("Bootstrap/GameCatalog");
            var settings = catalog.Defaults.CreateGrowthSettings();
            using var rig = RunTestRig.Create(enableAi: false, definitions: catalog.CreateDefinitions(), growthSettings: settings);
            rig.Upgrade(WeaponKind.Sword, UpgradeKind.Speed, 10);
            rig.Upgrade(WeaponKind.Sword, UpgradeKind.Range, 10);
            var definition = rig.Run.Definitions.GetWeapon(WeaponKind.Sword);
            var state = rig.Player.Weapons.Get(WeaponKind.Sword);
            var stats = StatCalculator.Calculate(definition, state, settings);
            Assert.That(stats.PeriodSeconds, Is.EqualTo(.4).Within(1e-12));
            Assert.That(stats.Range, Is.EqualTo(2.24).Within(1e-12));
            rig.Upgrade(WeaponKind.Sword, UpgradeKind.Speed);
            Assert.That(StatCalculator.Calculate(definition, state, settings).PeriodSeconds, Is.EqualTo(1 / 2.55).Within(1e-12));
            Assert.That(settings.CostForLevels(1, 1), Is.EqualTo(new BigInteger(5)));
            Assert.That(settings.CostForLevels(10, 1), Is.EqualTo(new BigInteger(32)));
            Assert.That(settings.CostForLevels(100, 1), Is.EqualTo(new BigInteger(302)));
            Assert.That(SsalMuk.Core.BossCharge.SpeedMultiplier, Is.EqualTo(5.2));
        }
        [Test]
        public void ExcessiveCopyCountsAndUnrepresentableDamageAreRejectedAtomically()
        {
            using var rig = RunTestRig.Create(); BigInteger large = BigInteger.Pow(10, 40);
            var state = rig.Player.Weapons.Get(WeaponKind.Sword);
            Assert.Throws<InvalidOperationException>(() => rig.Upgrade(WeaponKind.Sword, UpgradeKind.Copies, large));
            Assert.That(state.GetLevel(UpgradeKind.Copies), Is.EqualTo(BigInteger.Zero));
            Assert.That(StatCalculator.Calculate(rig.Run.Definitions.GetWeapon(WeaponKind.Sword), state).Copies, Is.EqualTo(BigInteger.One));
            Assert.Throws<NumericRangeException>(() => rig.Upgrade(WeaponKind.Sword, UpgradeKind.Damage, large));
            Assert.That(state.GetLevel(UpgradeKind.Damage), Is.EqualTo(BigInteger.Zero));
        }
    }
}
