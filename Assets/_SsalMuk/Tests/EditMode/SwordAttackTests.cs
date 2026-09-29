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
        public void NarrowSweepsRetainFastCrossingsAndPaddedBoundaryContacts()
        {
            foreach (double reach in new[] { 1.6, 17.088, 10000 })
                foreach (double start in new[] { 0.0, .25, .5, .75 })
                {
                    double end = start + .1;
                    double middle = (-.5 + (start + end) / 2) * Math.PI / 3;
                    DVec2 Local(double x, double y) => AttackGeometry.Rotate(new DVec2(x, y), middle);
                    // Both endpoints are outside the thin sweep; the path still crosses it.
                    Assert.That(AttackGeometry.SwordSweepContains(Local(reach * .5, -reach), Local(reach * .5, reach), .1, reach, start, end), Is.True);
                    foreach (var edge in new[] { Local(-.1, 0), Local(reach + .1, 0),
                        AttackGeometry.Rotate(new DVec2(reach * .5, 0), (-.5 + start) * Math.PI / 3),
                        AttackGeometry.Rotate(new DVec2(reach * .5, 0), (-.5 + end) * Math.PI / 3) })
                        Assert.That(AttackGeometry.SwordSweepContains(edge, edge, .1, reach, start, end), Is.True);
                    Assert.That(AttackGeometry.SwordSweepContains(Local(-.11, -reach), Local(-.11, reach), .1, reach, start, end), Is.False);
                }
        }

        [Test]
        public void NarrowSweepKeepsTheExistingAngularToleranceAtLongRange()
        {
            var point = new DVec2(17.064581489841444, .8943168211326535);
            Assert.That(AttackGeometry.SwordSweepContains(point, point, 0, 17.088, .45, .55), Is.True);
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

        [Test]
        public void CandidateReuseStillObservesWorldChangesBetweenCopySweeps()
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            var sword = new SwordAttack(rig.Run, rig.Movement, rig.Damage);
            void Sweep(double range = 1)
            {
                var attack = new AttackInstance(new HitKey(rig.Run.Id, rig.Run.AllocateAttackId(), 0, 0), WeaponKind.Sword,
                    0, .2, new DVec2(1, 0), new WeaponStats(8, range, 1), rig.Player.Position);
                sword.Step(attack, 0, .2, 0, .2);
            }
            long moving = rig.Spawn(UnitKind.Normal, new DVec2(1.99, .2), 100);
            Sweep(); Assert.That(rig.Unit(moving).Health, Is.EqualTo(100));
            // Movement inside the same spatial cell can enter the exact query circle.
            rig.World.MoveUnit(moving, WorldPosition.FromLocal(new DVec2(1.01, .2)));
            Sweep(); Assert.That(rig.Unit(moving).Health, Is.EqualTo(92));
            long added = rig.Spawn(UnitKind.Normal, new DVec2(.8, 0), 100);
            Sweep(); Assert.That(rig.Unit(added).Health, Is.EqualTo(92));
            rig.World.Units.Remove(added);
            long far = rig.Spawn(UnitKind.Normal, new DVec2(4, 0), 100);
            Sweep(); Assert.That(rig.Unit(far).Health, Is.EqualTo(100));
            Sweep(5); Assert.That(rig.Unit(far).Health, Is.EqualTo(92));
            rig.PlacePlayer(new DVec2(3.2, 0));
            Sweep(); Assert.That(rig.Unit(far).Health, Is.EqualTo(84));
        }
    }
}
