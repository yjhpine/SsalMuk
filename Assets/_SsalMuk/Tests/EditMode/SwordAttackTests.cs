using System;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class SwordAttackTests
    {
        [Test]
        public void SwordCoversForwardSixtyDegreesIncludingCircleAndArcEdges()
        {
            Assert.That(AttackGeometry.SwordContains(new DVec2(1, 0), 0, 2), Is.True);
            Assert.That(AttackGeometry.SwordContains(new DVec2(1, 1), 0, 2), Is.False);
            Assert.That(AttackGeometry.SwordContains(new DVec2(-1, 0), 0, 2), Is.False);
            Assert.That(AttackGeometry.SwordContains(new DVec2(Math.Sqrt(3), 1), 0, 2), Is.True);
            Assert.That(AttackGeometry.SwordContains(new DVec2(1, 1), 0.4, 2), Is.True);
            Assert.That(AttackGeometry.SwordContains(new DVec2(1.65, 0), 0.1, 1.6), Is.True);
            Assert.That(AttackGeometry.SwordContains(new DVec2(-0.1, 0), 0.2, 1.6), Is.True);
            Assert.That(AttackGeometry.SwordContains(new DVec2(2, 0), 0.1, 1.6), Is.False);
        }

        [Test]
        public void FastCircleCrossingTheSweptSectorIsNotMissed()
        {
            Assert.That(AttackGeometry.SwordSweepContains(new DVec2(1, -2), new DVec2(1, 2), 0.1, 1.6, 0, 1), Is.True);
            Assert.That(AttackGeometry.SwordSweepContains(new DVec2(-1, -2), new DVec2(-1, 2), 0.1, 1.6, 0, 1), Is.False);
        }

        [Test]
        public void OneSwingCanHitManyEnemiesButCannotHitOneEnemyTwice()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            long first = rig.Spawn(UnitKind.Normal, new DVec2(1, 0), 100);
            long second = rig.Spawn(UnitKind.Air, new DVec2(1.3, 0.1), 100);
            var definition = rig.Run.Definitions.GetWeapon(WeaponKind.Sword);
            var attack = new AttackInstance(new HitKey(rig.Run.Id, rig.Run.AllocateAttackId(), 0, 0), WeaponKind.Sword,
                0, definition.ActiveSeconds, new DVec2(1, 0), definition.BaseStats, rig.Player.Position);
            var sword = new SwordAttack(rig.Run, rig.Movement, rig.Damage);
            sword.Step(attack, 0, 0.2, 0, 0.2); sword.Step(attack, 0.2, 0.25, 0.2, 0.25);
            Assert.That(rig.Unit(first).Health, Is.EqualTo(92)); Assert.That(rig.Unit(second).Health, Is.EqualTo(92));
        }
    }
}
