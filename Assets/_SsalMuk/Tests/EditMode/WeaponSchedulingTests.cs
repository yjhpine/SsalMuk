using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class WeaponSchedulingTests
    {
        [Test]
        public void FractionalMeleeDurationsRetireEveryExpiredInstance()
        {
            foreach (var kind in new[] { WeaponKind.Spear, WeaponKind.Axe })
            {
                using var rig = RunTestRig.Create(enableAi: false);
                rig.Equip(kind); rig.Upgrade(kind, UpgradeKind.Speed, 10);
                rig.Spawn(UnitKind.Air, new DVec2(50, 0), 1000);
                using var runtime = new WeaponRuntime(rig.Run, kind, rig.Movement, rig.Damage);
                runtime.Tick(0, 20);
                Assert.That(runtime.LaunchCount, Is.GreaterThan(10));
                Assert.That(runtime.ActiveAttacks.All(a => a.StartedAt + a.ActiveSeconds > 20), Is.True,
                    kind + " retained attacks whose progress rounded below one.");
            }
        }
        [Test]
        public void CopyIndicesAlternateLeftRightWithoutIntegerTruncation()
        {
            Assert.That(CopyLayout.Phase(0), Is.Zero);
            Assert.That(CopyLayout.Phase(1), Is.EqualTo(Math.PI / 12).Within(1e-12));
            Assert.That(CopyLayout.Phase(2), Is.EqualTo(-Math.PI / 12).Within(1e-12));
            Assert.That(CopyLayout.Phase(3), Is.EqualTo(Math.PI / 6).Within(1e-12));
            Assert.That(CopyLayout.Phase(4), Is.EqualTo(-Math.PI / 6).Within(1e-12));
            BigInteger huge = BigInteger.Pow(10, 400);
            Assert.That(CopyLayout.Phase(huge), Is.EqualTo(CopyLayout.Phase(huge % 48)));
            Assert.That(CopyLayout.Phase(huge + 1), Is.EqualTo(CopyLayout.Phase((huge + 1) % 48)));
        }
        [Test]
        public void BurstReservationsKeepTheirTimesAndReadNewSpeedOnlyForTheNextGroup()
        {
            var scheduler = new AttackScheduler(); var current = new WeaponStats(8, 1.6, 1, repeats: 3);
            var first = scheduler.CollectDueStrikes(0, 0.1, () => current, true).ToArray();
            Assert.That(first.Length, Is.EqualTo(1)); Assert.That(first[0].Time, Is.Zero);
            current = new WeaponStats(8, 1.6, 0.5, repeats: 2);
            var later = scheduler.CollectDueStrikes(0.1, 1.4, () => current, true).ToArray();
            CollectionAssert.AreEqual(new BigInteger[] { 1, 2, 0, 1 }, later.Select(x => x.Repeat));
            CollectionAssert.AreEqual(new[] { 0.3, 0.6, 1.0, 1.3 }, later.Select(x => Math.Round(x.Time, 8)));
            Assert.That(scheduler.CollectDueStrikes(1.4, 100, () => current, false), Is.Empty);
            Assert.That(scheduler.CollectDueStrikes(100, 100.01, () => current, true).Single().Time, Is.EqualTo(100));
        }
        [Test]
        public void SchedulerKeepsAllDueStrikesInALongFrameAndDetectsUnrepresentableTiming()
        {
            var scheduler = new AttackScheduler(); var stats = new WeaponStats(8, 1.6, 0.1, repeats: 4);
            var due = scheduler.CollectDueStrikes(0, 0.25, () => stats, true).ToArray();
            Assert.That(due.Length, Is.EqualTo(11));
            Assert.That(due.Select(x => x.Time), Is.Ordered);
            Assert.Throws<NumericRangeException>(() => new AttackScheduler().CollectDueStrikes(1e20, 1e20 + 1e6, () => stats, true).ToArray());
            Assert.Throws<NumericRangeException>(() => new AttackScheduler().CollectDueStrikes(1, 1.02,
                () => new WeaponStats(8, 1, 1, repeats: BigInteger.Pow(10, 400)), true).ToArray());
        }
        [Test]
        public void RuntimeLaunchesEveryCopyAndReservedRepeatWithFreshStrikeStatsAndDirection()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            long target = rig.Spawn(UnitKind.Normal, new DVec2(4, 0), 1000);
            var current = new WeaponStats(8, 1.6, 1, repeats: 3); var launches = new List<AttackInstance>();
            using var runtime = new WeaponRuntime(rig.Run, WeaponKind.Sword, rig.Movement, rig.Damage, () => current);
            runtime.Launched += launches.Add; runtime.Tick(0, 0.1);
            current = new WeaponStats(16, 3.2, 0.5, copies: 3, repeats: 2); rig.World.MoveUnit(target, WorldPosition.FromLocal(new DVec2(-4, 0)));
            runtime.Tick(0.1, 1.4);
            Assert.That(launches.Count, Is.EqualTo(13)); Assert.That(launches.Select(x => x.Key.AttackId).Distinct().Count(), Is.EqualTo(13));
            Assert.That(launches[0].Stats.Damage, Is.EqualTo(8)); Assert.That(launches[0].Direction.X, Is.EqualTo(1));
            Assert.That(launches.Skip(1).All(x => x.Stats.Damage == 16 && x.Stats.Range == 3.2 &&
                Math.Abs(x.Direction.X + Math.Cos(CopyLayout.Phase(x.Key.Copy))) < 1e-10 &&
                Math.Abs(x.Direction.Y + Math.Sin(CopyLayout.Phase(x.Key.Copy))) < 1e-10), Is.True);
            CollectionAssert.AreEqual(new double[] { 0, 0.3, 0.3, 0.3, 0.6, 0.6, 0.6, 1, 1, 1, 1.3, 1.3, 1.3 }, launches.Select(x => Math.Round(x.StartedAt, 8)));
            CollectionAssert.AreEqual(new BigInteger[] { 0, 1, 2 }, launches.Skip(1).Take(3).Select(x => x.Key.Copy));
            Assert.That(launches[4].Key.Repeat, Is.EqualTo(new BigInteger(2)));
        }
        [Test]
        public void EachCopyDealsRealDamageAndCompletingOneDoesNotRetireTheOthers()
        {
            using var rig = RunTestRig.Create(enableAi: false); long target = rig.Spawn(UnitKind.Normal, new DVec2(1, 0), 100);
            using var runtime = new WeaponRuntime(rig.Run, WeaponKind.Sword, rig.Movement, rig.Damage, () => new WeaponStats(8, 1.6, 1, copies: 3));
            runtime.Tick(0, 0.3); Assert.That(rig.Unit(target).Health, Is.EqualTo(76));
            Assert.That(runtime.LaunchCount, Is.EqualTo(3)); Assert.That(rig.Damage.CachedHitCount, Is.Zero);
        }
        [Test]
        public void OwnedWeaponsRunTogetherAndSpeedCopiesAndRepeatsEachIncreaseRealLaunches()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            foreach (WeaponKind kind in Enum.GetValues(typeof(WeaponKind)))
            {
                if (kind != WeaponKind.Sword) rig.Equip(kind);
                rig.Upgrade(kind, UpgradeKind.Copies); rig.Upgrade(kind, UpgradeKind.Repeats); rig.Upgrade(kind, UpgradeKind.Speed, 10);
            }
            Assert.That(rig.Simulation.Weapons.Count, Is.EqualTo(4));
            long target = rig.Spawn(UnitKind.Normal, new DVec2(4, 0), 10000); rig.Movement.SetMoveIntent(target, DVec2.Zero);
            rig.Advance(0.5);
            Assert.That(rig.Simulation.Weapons[WeaponKind.Sword].LaunchCount, Is.EqualTo(6));
            Assert.That(rig.Simulation.Weapons[WeaponKind.Spear].LaunchCount, Is.EqualTo(4));
            Assert.That(rig.Simulation.Weapons[WeaponKind.Axe].LaunchCount, Is.EqualTo(4));
            Assert.That(rig.Simulation.Weapons[WeaponKind.Fireball].LaunchCount, Is.EqualTo(4));
            Assert.That(rig.Unit(target).Health, Is.LessThan(10000));
        }
        [TestCase(WeaponKind.Sword), TestCase(WeaponKind.Spear), TestCase(WeaponKind.Axe)]
        public void DamageAndRangeUpgradesReachPreviouslyMissedTargetsOnTheNextAttack(WeaponKind kind)
        {
            using var rig = RunTestRig.Create(enableAi: false); if (kind != WeaponKind.Sword) rig.Equip(kind);
            var definition = rig.Run.Definitions.GetWeapon(kind);
            long target = rig.Spawn(UnitKind.Normal, new DVec2(definition.Range * 1.6, 0), 100);
            using var runtime = new WeaponRuntime(rig.Run, kind, rig.Movement, rig.Damage);
            runtime.Tick(0, definition.ActiveSeconds + 0.01); Assert.That(rig.Unit(target).Health, Is.EqualTo(100));
            rig.Upgrade(kind, UpgradeKind.Range, 10); rig.Upgrade(kind, UpgradeKind.Damage, 10);
            runtime.Tick(definition.ActiveSeconds + 0.01, definition.PeriodSeconds + definition.ActiveSeconds + 0.01);
            Assert.That(rig.Unit(target).Health, Is.EqualTo(100 - definition.Damage * 2));
        }
        [Test]
        public void DeathClearsAllWeaponInstancesAndFlyingProjectilesBeforeRestart()
        {
            using var rig = RunTestRig.Create(enableAi: false); rig.Equip(WeaponKind.Fireball);
            rig.Spawn(UnitKind.Normal, new DVec2(20, 0), 1000); rig.Advance(0.02);
            var old = rig.Run; var simulation = rig.Simulation; var system = simulation.Weapons[WeaponKind.Fireball].Projectiles;
            Assert.That(system.Active.Count, Is.EqualTo(1)); var shot = system.Active[0];
            rig.Hit(rig.Player.Id, 1000); rig.Advance(0.02);
            Assert.That(system.Active, Is.Empty); Assert.That(shot.IsAlive, Is.False);
            Assert.That(simulation.Weapons.Values.All(x => x.ActiveAttacks.Count == 0), Is.True);
            rig.RestartAsync().GetAwaiter().GetResult();
            Assert.That(rig.Player.Weapons.Kinds, Is.EquivalentTo(new[] { WeaponKind.Sword }));
            system.Step(0.02, 1); Assert.That(rig.Player.Health, Is.EqualTo(100));
            Assert.That(rig.Simulation.Weapons[WeaponKind.Fireball].Projectiles.Active, Is.Empty);
            var stale = new AttackInstance(shot.Key, WeaponKind.Fireball, 0.02, 0.2, shot.Direction, shot.Stats, shot.Position);
            Assert.Throws<InvalidOperationException>(() => system.Launch(stale));
            Assert.Throws<ArgumentException>(() => rig.Simulation.Weapons[WeaponKind.Fireball].Projectiles.Launch(stale));
        }
    }
}
