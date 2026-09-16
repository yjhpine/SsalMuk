using System;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class DamageTests
    {
        [Test]
        public void AcceptedPlayerHitStartsShortInvulnerabilityAndRejectedHitsProduceNoEvent()
        {
            using var rig = RunTestRig.Create(); int events = 0; rig.Damage.Accepted += _ => events++;
            Assert.That(rig.Hit(rig.Player.Id, 3), Is.True);
            Assert.That(rig.Hit(rig.Player.Id, 3), Is.False);
            Assert.That(rig.Player.Health, Is.EqualTo(97)); Assert.That(events, Is.EqualTo(1));
            Assert.That(rig.Damage.TryApply(Request(rig, rig.Player.Id, 3), 0.249), Is.False);
            Assert.That(rig.Damage.TryApply(Request(rig, rig.Player.Id, 3), 0.25), Is.True);
            Assert.That(rig.Player.Health, Is.EqualTo(94)); Assert.That(events, Is.EqualTo(2));
        }

        [Test]
        public void OneHitKeyDamagesEachTargetOnceWithoutChangingTheSharedDefinition()
        {
            using var rig = RunTestRig.Create(); var definition = rig.Run.Definitions.GetUnit(UnitKind.Normal);
            var factory = new GroundEnemyFactory(rig.World.Units);
            var first = factory.Spawn(new UnitSpawnRequest(rig.Run.Id, UnitKind.Normal, WorldPosition.FromLocal(new DVec2(4, 0)), definition));
            var second = factory.Spawn(new UnitSpawnRequest(rig.Run.Id, UnitKind.Normal, WorldPosition.FromLocal(new DVec2(5, 0)), definition));
            var request = Request(rig, first.Id, 3);
            Assert.That(rig.Damage.TryApply(request, 0), Is.True);
            Assert.That(rig.Damage.TryApply(request, 1), Is.False);
            Assert.That(first.Health, Is.EqualTo(7)); Assert.That(second.Health, Is.EqualTo(10)); Assert.That(definition.MaxHealth, Is.EqualTo(10));
            Assert.That(rig.Damage.TryApply(new DamageRequest(request.Key, rig.Player.Id, second.Id, 3, DVec2.Zero), 0), Is.True);
            var copy = new HitKey(rig.Run.Id, request.Key.AttackId, 1, 0);
            Assert.That(rig.Damage.TryApply(new DamageRequest(copy, rig.Player.Id, first.Id, 3, DVec2.Zero), 0), Is.True);
            Assert.That(first.Health, Is.EqualTo(4)); Assert.That(second.Health, Is.EqualTo(7));
        }

        [Test]
        public void InvalidRunAndInvalidAmountsCannotDamageOrEmitFeedback()
        {
            using var rig = RunTestRig.Create(); long enemy = rig.Spawn(UnitKind.Normal, new DVec2(2, 0)); int events = 0;
            rig.Damage.Accepted += _ => events++;
            var oldKey = new HitKey(Guid.NewGuid(), 1, 0, 0);
            Assert.That(rig.Damage.TryApply(new DamageRequest(oldKey, rig.Player.Id, enemy, 3, DVec2.Zero), 0), Is.False);
            foreach (double amount in new[] { 0, -1, double.NaN, double.PositiveInfinity })
                Assert.That(rig.Damage.TryApply(Request(rig, enemy, amount), 0), Is.False);
            Assert.That(rig.Unit(enemy).Health, Is.EqualTo(10)); Assert.That(events, Is.Zero);
        }

        internal static DamageRequest Request(RunTestRig rig, long target, double amount, DVec2? direction = null) =>
            new DamageRequest(new HitKey(rig.Run.Id, rig.Run.AllocateAttackId(), 0, 0), rig.Player.Id, target, amount, direction ?? DVec2.Zero);

        [Test]
        public void LatestKnockbackReplacesItsDirectionAndAirKeepsItsOriginalFlight()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            long ground = rig.Spawn(UnitKind.Normal, new DVec2(4, 4)); rig.Movement.SetMoveIntent(ground, DVec2.Zero);
            Assert.That(rig.Damage.TryApply(Request(rig, ground, 1, new DVec2(1, 0)), 0), Is.True);
            Assert.That(rig.Damage.TryApply(Request(rig, ground, 1, new DVec2(0, 1)), 0), Is.True);
            Assert.That(rig.Unit(ground).Knockback.IsActive, Is.True);
            rig.Movement.Step(0.15);
            Assert.That(rig.Unit(ground).Position.Local.X, Is.EqualTo(4).Within(1e-8));
            Assert.That(rig.Unit(ground).Position.Local.Y, Is.EqualTo(4.3).Within(1e-8));
            Assert.That(rig.Unit(ground).Knockback.IsActive, Is.False);
            long air = rig.Spawn(UnitKind.Air, new DVec2(2, 1)); rig.Movement.SetAirDirection(air, new DVec2(1, 0));
            Assert.That(rig.Damage.TryApply(Request(rig, air, 1, new DVec2(0, 1)), 0), Is.True);
            rig.Movement.Step(0.15); var after = rig.Unit(air).Position;
            Assert.That(after.Local.Y, Is.EqualTo(1.3).Within(1e-8));
            rig.Movement.Step(0.2);
            Assert.That(after.DisplacementTo(rig.Unit(air).Position).X, Is.EqualTo(1.2).Within(1e-8));
            Assert.That(after.DisplacementTo(rig.Unit(air).Position).Y, Is.Zero.Within(1e-8));
        }
    }
}
