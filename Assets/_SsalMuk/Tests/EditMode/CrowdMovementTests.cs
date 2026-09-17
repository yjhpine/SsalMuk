using System;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class CrowdMovementTests
    {
        [Test]
        public void StationaryOverlappingGroundBodiesRemainStillWithoutDeletingUnits()
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false); rig.PlacePlayer(new DVec2(8, 0));
            long a = rig.Spawn(UnitKind.Normal, new DVec2(0, 0)); long b = rig.Spawn(UnitKind.Normal, new DVec2(0.1, 0));
            rig.Movement.SetMoveIntent(a, DVec2.Zero); rig.Movement.SetMoveIntent(b, DVec2.Zero);
            rig.Advance(0.2);
            Assert.That(rig.Unit(a).Position, Is.EqualTo(WorldPosition.FromLocal(DVec2.Zero)));
            Assert.That(rig.Unit(b).Position, Is.EqualTo(WorldPosition.FromLocal(new DVec2(0.1, 0))));
            Assert.That(rig.Unit(a).IsAlive && rig.Unit(b).IsAlive, Is.True);
        }

        [Test]
        public void IdenticalCentersSteerDeterministicallyAndAirIgnoresCrowdsAndWalls()
        {
            using var a = RunTestRig.Create(); using var b = RunTestRig.Create();
            a.PlacePlayer(new DVec2(10, 0)); b.PlacePlayer(new DVec2(10, 0));
            for (int i = 0; i < 5; i++) { a.Spawn(UnitKind.Normal, DVec2.Zero); b.Spawn(UnitKind.Normal, DVec2.Zero); }
            a.Advance(0.5); b.Advance(0.5);
            foreach (var unit in a.World.Units.Units) Assert.That(unit.Position, Is.EqualTo(b.Unit(unit.Id).Position));
            using var wall = NavigationTests.Layout((x, y) => x == 5);
            var player = (PlayerModel)WorldStoreTests.Spawn(wall, UnitKind.Player, NavigationTests.P(9, 4));
            var air = WorldStoreTests.Spawn(wall, UnitKind.Air, NavigationTests.P(2, 4));
            WorldStoreTests.Spawn(wall, UnitKind.Normal, NavigationTests.P(3, 4));
            var movement = new MovementSystem(wall, new NavigationService(wall), player);
            movement.SetAirDirection(air.Id, new DVec2(1, 0));
            for (int i = 0; i < 200; i++) movement.Step(0.02);
            Assert.That(air.Position.Local.X, Is.EqualTo(10).Within(1e-8));
        }

        [Test]
        public void SteeringNeverAddsSpeedAndAttackKnockbackKeepsItsDisplacement()
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            rig.PlacePlayer(new DVec2(10, 0));
            long a = rig.Spawn(UnitKind.Normal, DVec2.Zero), b = rig.Spawn(UnitKind.Normal, new DVec2(.1, 0));
            rig.Movement.SetMoveIntent(a, new DVec2(1, 0)); rig.Movement.SetMoveIntent(b, DVec2.Zero);
            rig.Advance(.02);
            Assert.That(rig.Unit(a).Position.DistanceTo(WorldPosition.FromLocal(DVec2.Zero)), Is.LessThanOrEqualTo(.03 + 1e-9));
            var start = rig.Unit(a).Position;
            rig.Movement.AddKnockback(a, new DVec2(1, 0), .1); rig.Advance(.1);
            Assert.That(start.DisplacementTo(rig.Unit(a).Position).X, Is.EqualTo(1).Within(1e-8));
            Assert.That(rig.Unit(b).Position, Is.EqualTo(WorldPosition.FromLocal(new DVec2(.1, 0))));
        }

        [Test]
        public void GroundChasesAroundWallWhileFarUnitsKeepMoving()
        {
            using var wall = NavigationTests.Layout((x, y) => x == 5 && y > 1 && y < 8);
            var player = (PlayerModel)WorldStoreTests.Spawn(wall, UnitKind.Player, NavigationTests.P(9.5, 4.5));
            var enemy = WorldStoreTests.Spawn(wall, UnitKind.Normal, NavigationTests.P(2.5, 4.5));
            var movement = new MovementSystem(wall, new NavigationService(wall), player);
            for (int i = 0; i < 600; i++) { movement.Step(0.02); Assert.That(wall.Query.IsCircleFree(enemy.Position, enemy.BodyRadius), Is.True); }
            Assert.That(enemy.Position.DistanceTo(player.Position), Is.LessThan(1.5));
            using var rig = RunTestRig.Create();
            long far = rig.Spawn(UnitKind.Normal, new DVec2(150, 16));
            double before = rig.Unit(far).Position.DistanceTo(rig.Player.Position);
            rig.Advance(2);
            Assert.That(rig.Unit(far).Position.DistanceTo(rig.Player.Position), Is.LessThan(before));
            Assert.That(rig.World.Units.Count, Is.EqualTo(2));
        }

        [Test]
        public void SlidingAndKnockbackCorrectionsDoNotPushCrowdsThroughWalls()
        {
            using var wall = NavigationTests.Layout((x, y) => x == 5);
            var start = NavigationTests.P(4.5, 2.5);
            var result = CircleSweep.MoveAndSlide(wall.Query, start, new DVec2(3, 2), 0.26);
            Assert.That(result.Local.X, Is.LessThanOrEqualTo(4.740001));
            Assert.That(result.Local.Y, Is.GreaterThan(4));
            var player = (PlayerModel)WorldStoreTests.Spawn(wall, UnitKind.Player, NavigationTests.P(4.5, 10));
            var movement = new MovementSystem(wall, new NavigationService(wall), player);
            for (int i = 0; i < 12; i++)
            {
                var unit = WorldStoreTests.Spawn(wall, UnitKind.Normal, NavigationTests.P(3.5 + (i % 3) * 0.3, 2.5 + (i / 3) * 0.3));
                movement.AddKnockback(unit.Id, new DVec2(4, 0), 0.15);
            }
            for (int i = 0; i < 30; i++) movement.Step(0.02);
            foreach (var unit in wall.Units.Units) Assert.That(wall.Query.IsCircleFree(unit.Position, unit.BodyRadius), Is.True);
            Assert.That(wall.Units.Count, Is.EqualTo(13));
        }

        [Test]
        public void BlockedCrowdMayStopWithoutTeleportingOrBouncing()
        {
            using var world = NavigationTests.Layout((x, y) => y == 3 || y == 5 || x == 8);
            var player = (PlayerModel)WorldStoreTests.Spawn(world, UnitKind.Player, NavigationTests.P(7.5, 4.5));
            var movement = new MovementSystem(world, new NavigationService(world), player);
            for (int i = 0; i < 8; i++) WorldStoreTests.Spawn(world, UnitKind.Normal, NavigationTests.P(2.5 + i * 0.55, 4.5));
            for (int step = 0; step < 100; step++)
            {
                movement.Step(0.02);
                foreach (var unit in world.Units.Units)
                {
                    Assert.That(world.Query.IsCircleFree(unit.Position, unit.BodyRadius), Is.True);
                    Assert.That(unit.Position.DistanceTo(movement.PreviousPositions[unit.Id]), Is.LessThan(0.25));
                }
            }
        }
    }
}
