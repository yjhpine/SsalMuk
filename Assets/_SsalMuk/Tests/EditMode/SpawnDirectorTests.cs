using System;
using System.Linq;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class SpawnDirectorTests
    {
        private static SpawnSettings BossOnly() => new SpawnSettings(normalBaseRate: 0.0001, normalRateGrowth: 0, airInterval: 10000,
            surroundWaves: new SurroundWaveSettings(baseCount: 0));
        [Test]
        public void BossesOverlapAndLaterDifficultyDoesNotHealAnExistingBoss()
        {
            using var rig = RunTestRig.Create(enableAi: false); using var director = new SpawnDirector(rig.Run, BossOnly());
            var bounds = new WorldRect(rig.Player.Position, 16, 9);
            director.Tick(300, bounds);
            Assert.That(director.SpawnedCount(UnitKind.Boss), Is.EqualTo(1));
            var first = rig.Run.Units.Single(x => x.Kind == UnitKind.Boss); rig.Hit(first.Id, 1); double health = first.Health;
            director.Tick(600, bounds); director.Tick(600, bounds);
            var bosses = rig.Run.Units.Where(x => x.Kind == UnitKind.Boss).ToArray(); Assert.That(bosses.Length, Is.EqualTo(2));
            Assert.That(first.Health, Is.EqualTo(health));
            Assert.That(bosses.Single(x => x.Id != first.Id).Definition.MaxHealth, Is.EqualTo(rig.Run.Definitions.GetUnit(UnitKind.Boss).MaxHealth * 2));
            Assert.That(bosses.All(x => !bounds.Contains(x.Position, x.BodyRadius) && rig.World.Query.IsCircleFree(x.Position, x.BodyRadius)), Is.True);
            Assert.That(director.Pending, Is.Empty);
        }
        [Test]
        public void ABlockedBossTicketSurvivesNewDeadlinesAndRetriesExactlyOnceWhenFloorOpens()
        {
            var terrain = new MutableTerrain(); using var rig = RunTestRig.Create(enableAi: false, terrain: terrain);
            using var director = new SpawnDirector(rig.Run, BossOnly()); var bounds = new WorldRect(rig.Player.Position, 16, 9);
            director.Tick(300, bounds); Assert.That(director.Pending.Count, Is.EqualTo(1)); long first = director.Pending[0].ScheduleId;
            director.Tick(600, bounds); Assert.That(director.Pending.Count, Is.EqualTo(2));
            Assert.That(director.Pending[0].ScheduleId, Is.EqualTo(first)); Assert.That(director.Pending.Select(x => x.ScheduleId).Distinct().Count(), Is.EqualTo(2));
            Assert.That(director.SpawnedCount(UnitKind.Boss), Is.Zero);
            terrain.Blocked = false; rig.World.ClearTerrainCache(); director.Tick(600, bounds); director.Tick(600, bounds);
            Assert.That(director.Pending, Is.Empty); Assert.That(director.SpawnedCount(UnitKind.Boss), Is.EqualTo(2));
        }
        [Test]
        public void TheCreationBudgetDefersWorkWithoutDiscardingAnyTimedSpawn()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            using var director = new SpawnDirector(rig.Run, new SpawnSettings(creationBudget: 2));
            var bounds = new WorldRect(rig.Player.Position, 16, 9); director.Tick(20, bounds);
            Assert.That(director.SpawnedCount(UnitKind.Normal) + director.SpawnedCount(UnitKind.Air), Is.EqualTo(2));
            Assert.That(director.Pending.Sum(x => x.Remaining), Is.EqualTo(38));
            for (int i = 0; i < 30 && director.Pending.Count > 0; i++) director.Tick(20, bounds);
            Assert.That(director.Pending, Is.Empty); Assert.That(director.SpawnedCount(UnitKind.Normal), Is.EqualTo(32));
            Assert.That(director.SpawnedCount(UnitKind.Air), Is.EqualTo(8));
            Assert.That(rig.Run.Units.Count, Is.EqualTo(41));
        }
        [Test]
        public void AirWaveStartsOutsideOneSideAndKeepsParallelFlightAfterPlayerMoves()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            using var director = new SpawnDirector(rig.Run, new SpawnSettings(normalBaseRate: 0.0001, normalRateGrowth: 0));
            var view = new WorldRect(rig.Player.Position, 16, 9); director.Tick(20, view);
            var units = rig.Run.Units.OfType<AirEnemyModel>().ToArray(); Assert.That(units.Length, Is.EqualTo(8));
            var positions = units.Select(x => x.Position).ToArray(); var direction = units[0].OriginalDirection;
            Assert.That(direction.Length, Is.EqualTo(1).Within(1e-10));
            Assert.That(units.All(x => x.OriginalDirection == direction && !view.Contains(x.Position, x.BodyRadius)), Is.True);
            var across = new DVec2(-direction.Y, direction.X);
            var offsets = positions.Select(position => positions[0].DisplacementTo(position)).ToArray();
            Assert.That(offsets.Select(offset => Math.Round(DVec2.Dot(offset, across), 6)).Distinct().Count(), Is.EqualTo(3));
            Assert.That(offsets.Max(offset => DVec2.Dot(offset, direction)) - offsets.Min(offset => DVec2.Dot(offset, direction)), Is.GreaterThan(3));
            rig.PlacePlayer(new DVec2(100, 100)); rig.Movement.Step(1);
            for (int i = 0; i < units.Length; i++)
                Assert.That((positions[i].DisplacementTo(units[i].Position) - direction * units[i].Definition.MoveSpeed).Length, Is.LessThan(1e-8));
            Assert.That(rig.Run.Units.OfType<AirEnemyModel>().Count(), Is.EqualTo(8));
        }

        [TestCase(0), TestCase(.25), TestCase(.5), TestCase(.75)]
        public void ThreeLaneAirCapsuleStartsOffscreenFromEverySide(double side)
        {
            var center = new WorldPosition(new ChunkCoord(9007199254740993, -50000), new DVec2(31.9, .1));
            var view = new WorldRect(center, 16, 9);
            var layout = new AirGroupSpawner(view, center, 24, .325, new SpawnSettings(), new FixedRandom(side));
            var positions = Enumerable.Range(0, 24).Select(index => layout.Position(index)).ToArray();
            Assert.That(positions.All(position => !view.Contains(position, .325)), Is.True);
            Assert.That(positions.Distinct().Count(), Is.EqualTo(24));
            var across = new DVec2(-layout.Direction.Y, layout.Direction.X);
            var lanes = positions.GroupBy(position => Math.Round(DVec2.Dot(positions[0].DisplacementTo(position), across), 6)).ToArray();
            Assert.That(lanes.Length, Is.EqualTo(3));
            var along = positions.Select(position => DVec2.Dot(positions[0].DisplacementTo(position), layout.Direction)).ToArray();
            Assert.That(along.Max() - along.Min(), Is.GreaterThan(2 * (lanes.Max(lane => lane.Key) - lanes.Min(lane => lane.Key))));
            foreach (var lane in lanes.Skip(1))
            {
                var depth = lane.Select(position => DVec2.Dot(positions[0].DisplacementTo(position), layout.Direction)).ToArray();
                Assert.That(depth.Max(), Is.LessThan(along.Max()));
                Assert.That(depth.Min(), Is.GreaterThan(along.Min()));
            }
        }

        private sealed class FixedRandom : IRandomSource
        {
            private readonly double value;
            public FixedRandom(double value) { this.value = value; }
            public double NextUnit() => value;
        }
        private sealed class MutableTerrain : IChunkGenerator
        {
            public bool Blocked = true;
            public ChunkData Generate(ChunkCoord coord)
            {
                var blocks = new bool[1024];
                if (Blocked) for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                    blocks[y * 32 + x] = new WorldPosition(coord, new DVec2(x + 0.5, y + 0.5)).DistanceTo(default) > 2;
                return new ChunkData(coord, blocks, 1);
            }
        }
    }
}
