using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class BodyHitSeparationTests
    {
        private static long Target(RunTestRig rig, DVec2 position, double hurtRadius)
        {
            var definition = new UnitDefinition("wide-hit", UnitKind.Normal, 100, 1, .1, 5, BigInteger.One, hurtRadius, .26);
            return new GroundEnemyFactory(rig.World.Units).Spawn(new UnitSpawnRequest(rig.Run.Id, UnitKind.Normal,
                WorldPosition.FromLocal(position), definition)).Id;
        }
        private static AttackInstance Attack(RunTestRig rig, WeaponKind kind)
        {
            var definition = rig.Run.Definitions.GetWeapon(kind);
            return new AttackInstance(new HitKey(rig.Run.Id, rig.Run.AllocateAttackId(), 0, 0), kind, 0,
                definition.ActiveSeconds, new DVec2(1, 0), definition.BaseStats, rig.Player.Position);
        }

        [TestCase(WeaponKind.Sword)]
        [TestCase(WeaponKind.Spear)]
        [TestCase(WeaponKind.Axe)]
        public void MeleeUsesHurtRadiusInCandidateSearchAndExactHit(WeaponKind kind)
        {
            using var rig = RunTestRig.Create(enableAi: false);
            var definition = rig.Run.Definitions.GetWeapon(kind);
            // Center is beyond the old body-based broad phase as well as the visible weapon.
            double x = definition.Range + definition.Width + .5;
            long wide = Target(rig, new DVec2(x, 0), 1.1);
            long narrow = Target(rig, new DVec2(x, 0), .1);
            var attack = Attack(rig, kind); double duration = definition.ActiveSeconds;
            if (kind == WeaponKind.Sword) new SwordAttack(rig.Run, rig.Movement, rig.Damage).Step(attack, 0, duration, 0, duration);
            else if (kind == WeaponKind.Spear) new SpearAttack(rig.Run, rig.Movement, rig.Damage).Step(attack, 0, duration, 0, duration);
            else new AxeAttack(rig.Run, rig.Movement, rig.Damage).Step(attack, 0, duration, 0, duration);
            Assert.That(rig.Unit(wide).Health, Is.EqualTo(100 - definition.Damage));
            Assert.That(rig.Unit(narrow).Health, Is.EqualTo(100));
            Assert.That(rig.Unit(wide).BodyRadius, Is.EqualTo(.1));
        }

        [Test]
        public void ProjectileContactAndExplosionUseHurtRadius()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            long first = Target(rig, new DVec2(2, 0), .6);
            long splash = Target(rig, new DVec2(1.3, 1.25), .6);
            long outside = Target(rig, new DVec2(1.3, 1.6), .6);
            using var projectiles = new ProjectileSystem(rig.Run, rig.Movement, rig.Damage);
            var shot = projectiles.Launch(Attack(rig, WeaponKind.Fireball)); projectiles.Step(0, 1);
            Assert.That(shot.HitTargetId, Is.EqualTo(first));
            Assert.That(shot.Position.Local.X, Is.EqualTo(1.3).Within(1e-8));
            Assert.That(rig.Unit(first).Health, Is.EqualTo(94));
            Assert.That(rig.Unit(splash).Health, Is.EqualTo(94));
            Assert.That(rig.Unit(outside).Health, Is.EqualTo(100));
        }

        [Test]
        public void LargeHurtAreaDoesNotEnlargeContactDamageOrFootAnchor()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            long enemy = Target(rig, new DVec2(.8, 0), 1.1);
            rig.Contact.Step(0, .02);
            Assert.That(rig.Player.Health, Is.EqualTo(100));
            Assert.That(rig.Unit(enemy).Definition.VisualFootOffset, Is.EqualTo(.26));
            rig.World.MoveUnit(enemy, WorldPosition.FromLocal(new DVec2(.3, 0)));
            rig.Contact.Step(.02, .04);
            Assert.That(rig.Player.Health, Is.EqualTo(95));
        }

        [Test]
        public void DifficultyCopiesIndependentRadiiAndRejectsInvalidGeometry()
        {
            var definition = new UnitDefinition("radii", UnitKind.Normal, 10, 1, .26, 5, BigInteger.One, .3575, .2);
            var scaled = new DifficultyCurve(new SpawnSettings()).AtSpawn(definition, 300);
            Assert.That(scaled.BodyRadius, Is.EqualTo(.26)); Assert.That(scaled.HurtRadius, Is.EqualTo(.3575));
            Assert.That(scaled.VisualFootOffset, Is.EqualTo(.2)); Assert.That(scaled.MaxHealth, Is.EqualTo(20));
            Assert.Throws<ArgumentOutOfRangeException>(() => new UnitDefinition("bad", UnitKind.Normal, 10, 1, .26, 5, BigInteger.One, -1));
        }

        [Test]
        public void ReusedSpatialBufferPreservesOrderAndDoesNotLeakOldResults()
        {
            var index = new SpatialIndex(); var origin = WorldPosition.FromLocal(new DVec2(-.1, 31.9));
            index.Upsert(9, origin); index.Upsert(3, origin.Offset(new DVec2(.2, .2)));
            var buffer = new List<long> { 999 };
            index.QueryCircle(origin, .5, buffer);
            CollectionAssert.AreEqual(index.QueryCircle(origin, .5), buffer);
            CollectionAssert.AreEqual(new long[] { 3, 9 }, buffer);
            index.QueryCircle(origin.Offset(new DVec2(100, 0)), .5, buffer); Assert.That(buffer, Is.Empty);
        }
    }
}
