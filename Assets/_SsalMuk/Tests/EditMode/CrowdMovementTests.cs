using System;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class CrowdMovementTests
    {
        [Test]
        public void StationaryNormalsSpreadSoftlyAndRetainSlightOverlap()
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false); rig.PlacePlayer(new DVec2(8, 0));
            long a = rig.Spawn(UnitKind.Normal, new DVec2(0, 0)); long b = rig.Spawn(UnitKind.Normal, new DVec2(0.1, 0));
            rig.Movement.SetMoveIntent(a, DVec2.Zero); rig.Movement.SetMoveIntent(b, DVec2.Zero);
            rig.Advance(0.2);
            double gap = rig.Unit(a).Position.DistanceTo(rig.Unit(b).Position);
            Assert.That(gap, Is.GreaterThan(.45).And.LessThan(.52));
            Assert.That(rig.Unit(a).Position.Local.Y, Is.EqualTo(0).Within(1e-9));
            Assert.That(rig.Unit(a).Position.DisplacementTo(rig.Unit(b).Position).X, Is.GreaterThan(0));
            Assert.That(rig.Unit(a).IsAlive && rig.Unit(b).IsAlive, Is.True);
        }

        [Test]
        public void IdenticalCentersSpreadDeterministicallyAndAirIgnoresCrowdsAndWalls()
        {
            using var a = RunTestRig.Create(enableAi: false, enableCombat: false);
            using var b = RunTestRig.Create(enableAi: false, enableCombat: false);
            a.PlacePlayer(new DVec2(10, 0)); b.PlacePlayer(new DVec2(10, 0));
            long firstId = 0, lastId = 0;
            for (int i = 0; i < 5; i++)
            {
                long id = a.Spawn(UnitKind.Normal, DVec2.Zero), otherId = b.Spawn(UnitKind.Normal, DVec2.Zero);
                a.Movement.SetMoveIntent(id, DVec2.Zero); b.Movement.SetMoveIntent(otherId, DVec2.Zero);
                if (i == 0) firstId = id; lastId = id;
            }
            a.Advance(0.5); b.Advance(0.5);
            foreach (var unit in a.World.Units.Units) Assert.That(unit.Position, Is.EqualTo(b.Unit(unit.Id).Position));
            Assert.That(a.Unit(firstId).Position.DistanceTo(a.Unit(lastId).Position), Is.GreaterThan(.2));
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
        public void SoftSeparationIsBoundedAndDoesNotAlterAttackKnockback()
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            rig.PlacePlayer(new DVec2(10, 0));
            long a = rig.Spawn(UnitKind.Normal, DVec2.Zero), b = rig.Spawn(UnitKind.Normal, new DVec2(.1, 0));
            rig.Movement.SetMoveIntent(a, new DVec2(1, 0)); rig.Movement.SetMoveIntent(b, DVec2.Zero);
            rig.Advance(.02);
            Assert.That(rig.Unit(a).Position.DistanceTo(WorldPosition.FromLocal(DVec2.Zero)), Is.LessThanOrEqualTo(.075 + 1e-9));
            var start = rig.Unit(a).Position;
            rig.Movement.AddKnockback(a, new DVec2(1, 0), .1); rig.Advance(.1);
            Assert.That(start.DisplacementTo(rig.Unit(a).Position).X, Is.EqualTo(1).Within(1e-8));
            Assert.That(rig.Unit(b).Knockback.IsActive, Is.False);
            Assert.That(rig.Unit(b).IsAlive, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BossPushesNormalsAsideWithoutChangingItsChaseOrCharge(bool charging)
        {
            using var crowd = RunTestRig.Create(enableAi: false, enableCombat: false);
            using var clear = RunTestRig.Create(enableAi: false, enableCombat: false);
            crowd.PlacePlayer(new DVec2(10, 0)); clear.PlacePlayer(new DVec2(10, 0));
            long boss = crowd.Spawn(UnitKind.Boss, DVec2.Zero);
            long control = clear.Spawn(UnitKind.Boss, DVec2.Zero);
            crowd.Movement.Step(.5); clear.Movement.Step(.5);
            if (!charging)
            {
                ((GroundEnemyModel)crowd.Unit(boss)).Charge.Cancel();
                ((GroundEnemyModel)clear.Unit(control)).Charge.Cancel();
            }
            long normal = crowd.Spawn(UnitKind.Normal, new DVec2(.6, .15));
            crowd.Movement.SetMoveIntent(normal, DVec2.Zero);
            for (int i = 0; i < 10; i++)
            {
                crowd.Movement.Step(.02); clear.Movement.Step(.02);
                Assert.That(crowd.Unit(boss).Position, Is.EqualTo(clear.Unit(control).Position));
                Assert.That(crowd.Movement.GetEnemyFsm(boss).CurrentState,
                    Is.EqualTo(clear.Movement.GetEnemyFsm(control).CurrentState));
            }
            Assert.That(crowd.Unit(normal).Position.Local.Y, Is.GreaterThan(.3));
            Assert.That(crowd.Unit(normal).Health, Is.EqualTo(10));
            Assert.That(crowd.Unit(normal).Knockback.IsActive, Is.False);
            Assert.That(crowd.Player.Position, Is.EqualTo(WorldPosition.FromLocal(new DVec2(10, 0))));
        }

        [Test]
        public void BossPushCoversTheChargePathBetweenTicks()
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            rig.PlacePlayer(new DVec2(10, 0));
            long boss = rig.Spawn(UnitKind.Boss, DVec2.Zero);
            rig.Movement.Step(.5);
            long normal = rig.Spawn(UnitKind.Normal, new DVec2(1.4, .1));
            rig.Movement.SetMoveIntent(normal, DVec2.Zero);
            rig.Movement.Step(.2);
            Assert.That(rig.Unit(boss).Position.Local.X, Is.EqualTo(3.12).Within(1e-9));
            Assert.That(rig.Unit(normal).Position.Local.Y, Is.GreaterThan(.5));
            Assert.That(rig.Movement.PreviousPositions[normal], Is.EqualTo(WorldPosition.FromLocal(new DVec2(1.4, .1))));
        }

        [Test]
        public void BossDoesNotPushAirOrDriveNormalsThroughTerrain()
        {
            using var world = NavigationTests.Layout((x, y) => y == 3);
            var player = (PlayerModel)WorldStoreTests.Spawn(world, UnitKind.Player, NavigationTests.P(10, 2.24));
            var boss = WorldStoreTests.Spawn(world, UnitKind.Boss, NavigationTests.P(3, 2.24));
            var normal = WorldStoreTests.Spawn(world, UnitKind.Normal, NavigationTests.P(3.3, 2.7));
            var air = WorldStoreTests.Spawn(world, UnitKind.Air, NavigationTests.P(3.3, 2.7));
            using var movement = new MovementSystem(world, new NavigationService(world), player);
            movement.SetMoveIntent(normal.Id, DVec2.Zero); movement.SetAirDirection(air.Id, new DVec2(1, 0));
            var airStart = air.Position;
            for (int i = 0; i < 40; i++)
            {
                movement.Step(.02);
                Assert.That(world.Query.IsCircleFree(normal.Position, normal.BodyRadius), Is.True);
                Assert.That(world.Query.IsCircleFree(boss.Position, boss.BodyRadius), Is.True);
            }
            Assert.That(airStart.DisplacementTo(air.Position).Y, Is.EqualTo(0).Within(1e-9));
            Assert.That(airStart.DisplacementTo(air.Position).X, Is.EqualTo(air.MoveSpeed * .8).Within(1e-9));
            Assert.That(normal.Position.Local.Y, Is.GreaterThan(2.7));
            Assert.That(boss.Position.Local.X, Is.GreaterThan(4));
        }

        [Test]
        public void SeparationCrossesChunkBoundariesWithoutMovingPlayer()
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            long a = rig.Spawn(UnitKind.Normal, DVec2.Zero), b = rig.Spawn(UnitKind.Normal, DVec2.Zero);
            var origin = new WorldPosition(new ChunkCoord(9007199254740993, -50000), new DVec2(31.95, 4));
            rig.World.MoveUnit(a, origin); rig.World.MoveUnit(b, origin.Offset(new DVec2(.1, 0)));
            rig.World.MoveUnit(rig.Player.Id, origin);
            rig.Movement.SetMoveIntent(a, DVec2.Zero); rig.Movement.SetMoveIntent(b, DVec2.Zero);
            rig.Advance(.2);
            Assert.That(rig.Unit(a).Position.DistanceTo(rig.Unit(b).Position), Is.GreaterThan(.45));
            Assert.That(rig.Player.Position, Is.EqualTo(origin));
            Assert.That(rig.World.Query.QueryCircle(rig.Unit(a).Position, .01), Does.Contain(a));
            Assert.That(rig.World.Query.QueryCircle(rig.Unit(b).Position, .01), Does.Contain(b));
        }

        [Test]
        public void GroundStopsAtAWallWithoutRouteSearchWhileFarUnitsKeepMoving()
        {
            using var wall = NavigationTests.Layout((x, y) => x == 5 && y > 1 && y < 8);
            var player = (PlayerModel)WorldStoreTests.Spawn(wall, UnitKind.Player, NavigationTests.P(9.5, 4.5));
            var enemy = WorldStoreTests.Spawn(wall, UnitKind.Normal, NavigationTests.P(2.5, 4.5));
            var movement = new MovementSystem(wall, new NavigationService(wall), player);
            for (int i = 0; i < 600; i++) { movement.Step(0.02); Assert.That(wall.Query.IsCircleFree(enemy.Position, enemy.BodyRadius), Is.True); }
            Assert.That(enemy.Position.Local.X, Is.LessThan(5));
            Assert.That(enemy.Position.Local.Y, Is.EqualTo(4.5).Within(1e-8));
            Assert.That(movement.Navigation.PendingRequestCount, Is.Zero);
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
