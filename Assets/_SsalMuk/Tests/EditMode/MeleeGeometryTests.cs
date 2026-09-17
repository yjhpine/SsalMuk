using System;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class MeleeGeometryTests
    {
        [Test]
        public void SpearThrustRespectsForwardLengthWidthAndRelativeCrossings()
        {
            Assert.That(AttackGeometry.SpearSweepContains(new DVec2(2, 0.16), new DVec2(2, 0.16), 0, 2.2, 0.35, 0, 1), Is.True);
            Assert.That(AttackGeometry.SpearSweepContains(new DVec2(2, 0.19), new DVec2(2, 0.19), 0, 2.2, 0.35, 0, 1), Is.False);
            Assert.That(AttackGeometry.SpearSweepContains(new DVec2(-0.5, 0), new DVec2(-0.5, 0), 0.1, 2.2, 0.35, 0, 1), Is.False);
            Assert.That(AttackGeometry.SpearSweepContains(new DVec2(2.6, 0), new DVec2(2.6, 0), 0.1, 2.2, 0.35, 0, 1), Is.False);
            Assert.That(AttackGeometry.SpearSweepContains(new DVec2(1.1, -4), new DVec2(1.1, 4), 0.1, 2.2, 0.35, 0, 1), Is.True);
        }
        [Test]
        public void AxeCoversTheCircularBladePathButExcludesTheCenterAndOutside()
        {
            foreach (var point in new[] { new DVec2(1.8, 0), new DVec2(0, 1.8), new DVec2(-1.8, 0), new DVec2(0, -1.8) })
                Assert.That(AttackGeometry.AxeSweepContains(point, point, 0.1, 1.8, 0.25, 0, Math.PI * 2), Is.True);
            Assert.That(AttackGeometry.AxeSweepContains(DVec2.Zero, DVec2.Zero, 0.1, 1.8, 0.25, 0, Math.PI * 2), Is.False);
            Assert.That(AttackGeometry.AxeSweepContains(new DVec2(3, 0), new DVec2(3, 0), 0.1, 1.8, 0.25, 0, Math.PI * 2), Is.False);
            Assert.That(AttackGeometry.AxeSweepContains(new DVec2(-1.8, 0), new DVec2(-1.8, 0), 0.1, 1.8, 0.25, 0, Math.PI / 2), Is.False);
            Assert.That(AttackGeometry.AxeSweepContains(new DVec2(1.8, -3), new DVec2(1.8, 3), 0.1, 1.8, 0.25, -0.05, 0.05), Is.True);
        }
        [Test]
        public void SpearAndAxeUseRealDamageOncePerInstanceAndCopyAngles()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            long front = rig.Spawn(UnitKind.Normal, new DVec2(2.1, 0), 100);
            long back = rig.Spawn(UnitKind.Normal, new DVec2(-1.8, 0), 100);
            long middle = rig.Spawn(UnitKind.Normal, new DVec2(0.7, 0), 100);
            var spear = Create(rig, WeaponKind.Spear, 0); var executor = new SpearAttack(rig.Run, rig.Movement, rig.Damage);
            executor.Step(spear, 0, 0.18, 0, 0.18); executor.Step(spear, 0, 0.18, 0, 0.18);
            Assert.That(rig.Unit(front).Health, Is.EqualTo(90)); Assert.That(rig.Unit(back).Health, Is.EqualTo(100));
            var axe = Create(rig, WeaponKind.Axe, 0); var axeExecutor = new AxeAttack(rig.Run, rig.Movement, rig.Damage);
            axeExecutor.Step(axe, 0, 0.6, 0, 0.6); axeExecutor.Step(axe, 0, 0.6, 0, 0.6);
            Assert.That(rig.Unit(back).Health, Is.EqualTo(94)); Assert.That(rig.Unit(middle).Health, Is.EqualTo(90));
            long shifted = rig.Spawn(UnitKind.Normal, new DVec2(1.8 * Math.Cos(Math.PI / 6), 1.8 * Math.Sin(Math.PI / 6)), 100);
            var copy = Create(rig, WeaponKind.Spear, 3); executor.Step(copy, 0, 0.18, 0, 0.18);
            Assert.That(rig.Unit(shifted).Health, Is.EqualTo(90));
        }
        private static AttackInstance Create(RunTestRig rig, WeaponKind kind, int copy)
        {
            var definition = rig.Run.Definitions.GetWeapon(kind);
            return new AttackInstance(new HitKey(rig.Run.Id, rig.Run.AllocateAttackId(), copy, 0), kind, 0, definition.ActiveSeconds,
                new DVec2(1, 0), definition.BaseStats, rig.Player.Position);
        }
        [Test]
        public void AProjectileChoosesEarliestContactThenStableIdForEqualDistances()
        {
            var targets = new[] { new TargetCircle(20, new DVec2(4, 0), 0.3), new TargetCircle(10, new DVec2(1, 0), 0.3) };
            Assert.That(AttackGeometry.FirstCircleHit(DVec2.Zero, new DVec2(6, 0), targets), Is.EqualTo(10));
            var tied = new[] { new TargetCircle(9, new DVec2(1, 0), 0.3), new TargetCircle(2, new DVec2(1, 0), 0.3) };
            Assert.That(AttackGeometry.FirstCircleHit(DVec2.Zero, new DVec2(6, 0), tied), Is.EqualTo(2));
        }
    }
}
