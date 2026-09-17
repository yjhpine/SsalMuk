using System;
using System.Linq;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class MovementOptimizationTests
    {
        [Test]
        public void OrdinaryGroundDirectionIsReusedDuringItsDecisionCadenceWhileMotionContinues()
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            rig.PlacePlayer(new DVec2(0.85, 0.15));
            long enemy = rig.Spawn(UnitKind.Normal, new DVec2(0.15, 0.15));

            rig.Movement.Step(0.02);
            var afterFirst = rig.Unit(enemy).Position;
            rig.PlacePlayer(new DVec2(0.15, 0.85));
            rig.Movement.Step(0.02);

            var secondStep = afterFirst.DisplacementTo(rig.Unit(enemy).Position);
            Assert.That(secondStep.X, Is.GreaterThan(0.02));
            Assert.That(Math.Abs(secondStep.Y), Is.LessThan(1e-9));
            Assert.That(secondStep.Length, Is.EqualTo(0.03).Within(1e-9));
        }

        [Test]
        public void CrossingATargetCellRefreshesGroundDirectionImmediately()
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            rig.PlacePlayer(new DVec2(0.85, 0.15));
            long enemy = rig.Spawn(UnitKind.Normal, new DVec2(0.15, 0.15));

            rig.Movement.Step(0.02);
            var afterFirst = rig.Unit(enemy).Position;
            rig.PlacePlayer(new DVec2(0.15, 1.15));
            rig.Movement.Step(0.02);

            var secondStep = afterFirst.DisplacementTo(rig.Unit(enemy).Position);
            Assert.That(secondStep.Y, Is.GreaterThan(0.02));
            Assert.That(secondStep.X, Is.LessThan(0));
            Assert.That(secondStep.Length, Is.EqualTo(0.03).Within(1e-9));
        }

        [Test]
        public void CompletedFallbackRouteDoesNotBypassDirectDirectionCadence()
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            rig.PlacePlayer(new DVec2(0.85, 0.15));
            long enemy = rig.Spawn(UnitKind.Normal, new DVec2(20.15, 0.15));
            rig.Movement.Step(0.02);
            for (int i = 0; i < 1000 && rig.Navigation.PendingRequestCount > 0; i++) rig.Navigation.Advance(256);
            Assert.That(rig.Navigation.PendingRequestCount, Is.Zero);
            rig.World.MoveUnit(enemy, WorldPosition.FromLocal(new DVec2(0.15, 0.15)));

            rig.Movement.Step(0.02);
            var afterDirectDecision = rig.Unit(enemy).Position;
            rig.PlacePlayer(new DVec2(0.15, 0.85));
            rig.Movement.Step(0.02);

            var secondStep = afterDirectDecision.DisplacementTo(rig.Unit(enemy).Position);
            Assert.That(secondStep.X, Is.GreaterThan(0.02));
            Assert.That(Math.Abs(secondStep.Y), Is.LessThan(1e-9));
        }

        [Test]
        public void PassingAFlowAimRefreshesBeforeTheNextMovementStep()
        {
            using var world = NavigationTests.Layout((x, y) => !((x == 0 && y == 0) || (x == 1 && y >= 0 && y <= 2)));
            var player = (PlayerModel)WorldStoreTests.Spawn(world, UnitKind.Player, NavigationTests.P(1.5, 2.5));
            var enemy = WorldStoreTests.Spawn(world, UnitKind.Normal, NavigationTests.P(0.9, 0.5));
            var navigation = new NavigationService(world);
            using var movement = new MovementSystem(world, navigation, player);
            var field = navigation.GetFlowField(player.Position, enemy.BodyRadius);
            for (int i = 0; i < 1000 && !field.IsComplete; i++) navigation.Advance(256);
            Assert.That(field.IsComplete, Is.True);

            movement.Step(0.405);
            Assert.That(enemy.Position.Local.X, Is.GreaterThan(1.5));
            var afterPassingAim = enemy.Position;
            movement.Step(0.02);

            var nextStep = afterPassingAim.DisplacementTo(enemy.Position);
            Assert.That(nextStep.Y, Is.GreaterThan(0.03));
            Assert.That(nextStep.X, Is.LessThanOrEqualTo(0));
        }

        [Test]
        public void KnockbackEndResumesGroundDecisionInTheSameStep()
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            rig.PlacePlayer(new DVec2(2, 3.5));
            long enemy = rig.Spawn(UnitKind.Normal, new DVec2(2, 2));
            var start = rig.Unit(enemy).Position;

            rig.Movement.AddKnockback(enemy, new DVec2(1, 0), 0.01);
            rig.Movement.Step(0.02);

            var displacement = start.DisplacementTo(rig.Unit(enemy).Position);
            Assert.That(rig.Unit(enemy).Knockback.IsActive, Is.False);
            Assert.That(displacement.X, Is.EqualTo(1).Within(1e-9));
            Assert.That(displacement.Y, Is.EqualTo(0.015).Within(1e-9));
            Assert.That(rig.Movement.GetEnemyFsm(enemy).CurrentState, Is.EqualTo(EnemyState.Chase));
            var afterKnockback = rig.Unit(enemy).Position;

            rig.Movement.Step(0.02);

            var nextStep = afterKnockback.DisplacementTo(rig.Unit(enemy).Position);
            Assert.That(nextStep.X, Is.LessThan(-0.01));
            Assert.That(nextStep.Y, Is.GreaterThan(0.01));
            Assert.That(nextStep.Length, Is.EqualTo(0.03).Within(1e-9));
        }

        [Test]
        public void DenseGroundSteeringIsDeterministicAndNeverAddsSpeed()
        {
            using var first = RunTestRig.Create(enableAi: false, enableCombat: false);
            using var second = RunTestRig.Create(enableAi: false, enableCombat: false);
            first.PlacePlayer(new DVec2(20, 20)); second.PlacePlayer(new DVec2(20, 20));
            for (int i = 0; i < 128; i++)
            {
                var position = new DVec2(4 + (i % 16) * 0.02, 4 + (i / 16) * 0.02);
                long a = first.Spawn(UnitKind.Normal, position);
                long b = second.Spawn(UnitKind.Normal, position);
                first.Movement.SetMoveIntent(a, new DVec2(1, 0));
                second.Movement.SetMoveIntent(b, new DVec2(1, 0));
            }

            first.Movement.Step(0.02); second.Movement.Step(0.02);

            foreach (var unit in first.World.Units.Units.Where(candidate => candidate.Kind == UnitKind.Normal))
            {
                Assert.That(unit.Position, Is.EqualTo(second.Unit(unit.Id).Position));
                Assert.That(first.Movement.PreviousPositions[unit.Id].DistanceTo(unit.Position), Is.LessThanOrEqualTo(0.03 + 1e-9));
            }
        }
    }
}
