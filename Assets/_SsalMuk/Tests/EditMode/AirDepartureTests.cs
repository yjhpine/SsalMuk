using System;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class AirDepartureTests
    {
        private static AirEnemyModel Spawn(RunTestRig rig, WorldPosition position, DVec2 direction)
        {
            var definition = new UnitDefinition("air-exit", UnitKind.Air, 1000000, 6, .22, 0, BigInteger.One);
            return (AirEnemyModel)new AirEnemyFactory(rig.World.Units).Spawn(new UnitSpawnRequest(rig.Run.Id, UnitKind.Air, position, definition, direction));
        }

        [TestCase(1, 0, 16)]
        [TestCase(-1, 0, 16)]
        [TestCase(0, 1, 9)]
        [TestCase(0, -1, 9)]
        public void OutgoingAirLeavesWithoutDeathRewardsOrStaleReferences(double x, double y, double edge)
        {
            using var rig = RunTestRig.Create(enableAi: false);
            var direction = new DVec2(x, y);
            var air = Spawn(rig, rig.Player.Position.Offset(direction * (edge + 4 + .22 + .1)), direction);
            int removed = 0; rig.World.Units.Removed += unit => { if (unit.Id == air.Id) removed++; };
            rig.Advance(.04);
            Assert.That(rig.World.Units.TryGet(air.Id, out _), Is.False);
            Assert.That(air.IsAlive, Is.True, "Departure must not fabricate a death.");
            Assert.That(removed, Is.EqualTo(1));
            Assert.That(rig.Simulation.DepartedAirCount, Is.EqualTo(1));
            Assert.That(rig.Run.Kills, Is.Zero); Assert.That(rig.Run.Experience, Is.Empty);
            Assert.That(rig.World.Query.QueryCircle(air.Position, 1), Has.No.Member(air.Id));
            Assert.That(rig.Movement.PreviousPositions.ContainsKey(air.Id), Is.False);
            Assert.Throws<ArgumentException>(() => rig.Movement.GetEnemyFsm(air.Id));
            Assert.That(rig.Hit(air.Id, 1), Is.False);
        }

        [TestCase(1, 0, 16)]
        [TestCase(-1, 0, 16)]
        [TestCase(0, 1, 9)]
        [TestCase(0, -1, 9)]
        public void IncomingAirIsKeptOutsideTheSameMargin(double x, double y, double edge)
        {
            using var rig = RunTestRig.Create(enableAi: false);
            var direction = new DVec2(x, y);
            var air = Spawn(rig, rig.Player.Position.Offset(-direction * (edge + 10)), direction);
            var before = air.Position; rig.Advance(.02);
            Assert.That(rig.World.Units.TryGet(air.Id, out _), Is.True);
            Assert.That(before.DistanceTo(air.Position), Is.EqualTo(.12).Within(1e-8));
        }

        [Test]
        public void AirStaysUntilItsWholeBodyClearsTheScreenMargin()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            var air = Spawn(rig, rig.Player.Position.Offset(new DVec2(16 + 4 + .22 - .2, 0)), new DVec2(1, 0));
            rig.Advance(.02); Assert.That(rig.World.Units.TryGet(air.Id, out _), Is.True);
            rig.Advance(.02); Assert.That(rig.World.Units.TryGet(air.Id, out _), Is.False);
        }

        [Test]
        public void GroundBossAndExperienceRemainFarAway()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            long ground = rig.Spawn(UnitKind.Normal, new DVec2(64, 64), 1000);
            long boss = rig.Spawn(UnitKind.Boss, new DVec2(-64, -64), 1000);
            long xp = rig.DropXp(new DVec2(128, 128), 25);
            var air = Spawn(rig, rig.Player.Position.Offset(new DVec2(64, 0)), new DVec2(1, 0));
            rig.Advance(.02);
            Assert.That(rig.World.Units.TryGet(air.Id, out _), Is.False);
            Assert.That(rig.World.Units.TryGet(ground, out _), Is.True);
            Assert.That(rig.World.Units.TryGet(boss, out _), Is.True);
            Assert.That(rig.World.TryGetExperience(xp, out var orb), Is.True);
            Assert.That(orb.Value, Is.EqualTo(new BigInteger(25)));
        }

        [Test]
        public void KnockbackFinishesBeforeAirDeparts()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            var air = Spawn(rig, rig.Player.Position.Offset(new DVec2(22, 0)), new DVec2(1, 0));
            rig.Movement.AddKnockback(air.Id, new DVec2(-.1, 0), .2);
            rig.Advance(.1); Assert.That(rig.World.Units.TryGet(air.Id, out _), Is.True);
            rig.Advance(.12); Assert.That(rig.World.Units.TryGet(air.Id, out _), Is.False);
        }

        [Test]
        public void ActualAirDeathStillDropsExperienceAndCountsAKill()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            var air = Spawn(rig, rig.Player.Position.Offset(new DVec2(22, 0)), new DVec2(1, 0));
            Assert.That(rig.Hit(air.Id, 2000000), Is.True);
            rig.Advance(.02);
            Assert.That(rig.World.Units.TryGet(air.Id, out _), Is.False);
            Assert.That(rig.Run.Kills, Is.EqualTo(1));
            Assert.That(rig.Simulation.DepartedAirCount, Is.Zero);
            Assert.That(rig.Run.Experience.Count, Is.EqualTo(1));
        }

        [Test]
        public void DepartureUsesCurrentCameraBoundsAndLargeWorldCoordinates()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            var center = new WorldPosition(new ChunkCoord(1000000000000, -1000000000000), new DVec2(31.9, 31.9));
            rig.World.MoveUnit(rig.Player.Id, center);
            var air = Spawn(rig, center.Offset(new DVec2(24, 0)), new DVec2(1, 0));
            rig.Simulation.SetViewBounds(new WorldRect(center, 30, 17)); rig.Simulation.Step(.02);
            Assert.That(rig.World.Units.TryGet(air.Id, out _), Is.True);
            rig.Simulation.SetViewBounds(new WorldRect(center, 16, 9)); rig.Simulation.Step(.02);
            Assert.That(rig.World.Units.TryGet(air.Id, out _), Is.False);
        }

        [Test]
        public void AWholeParallelWaveEntersCrossesAndLeaves()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            var view = new WorldRect(rig.Player.Position, 16, 9);
            var layout = new AirGroupSpawner(view, rig.Player.Position, 24, .22, new SpawnSettings(), rig.Run.Streams.Spawn);
            var wave = Enumerable.Range(0, 24).Select(i => Spawn(rig, layout.Position(i), layout.Direction)).ToArray();
            rig.Advance(.02); Assert.That(wave.All(air => rig.World.Units.TryGet(air.Id, out _)), Is.True);
            rig.Advance(12);
            Assert.That(wave.Any(air => rig.World.Units.TryGet(air.Id, out _)), Is.False);
            Assert.That(rig.Simulation.DepartedAirCount, Is.EqualTo(24));
            Assert.That(rig.Run.Kills, Is.Zero); Assert.That(rig.Run.Experience, Is.Empty);
        }

        [Test]
        public void DepartureMarginComesFromRunConfiguration()
        {
            using var rig = RunTestRig.Create(enableAi: false, spawnSettings: new SpawnSettings(airDepartureMargin: 10));
            var air = Spawn(rig, rig.Player.Position.Offset(new DVec2(22, 0)), new DVec2(1, 0));
            rig.Advance(.02); Assert.That(rig.World.Units.TryGet(air.Id, out _), Is.True);
            rig.Advance(1); Assert.That(rig.World.Units.TryGet(air.Id, out _), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpawnSettings(airDepartureMargin: double.NaN));
        }
    }
}
