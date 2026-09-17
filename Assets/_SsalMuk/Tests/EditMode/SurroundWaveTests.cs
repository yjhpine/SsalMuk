using System;
using System.Linq;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class SurroundWaveTests
    {
        private static SpawnSettings WaveOnly(int cap = 500, int budget = 64) =>
            new SpawnSettings(normalBaseRate: .0001, normalRateGrowth: 0, airInterval: 10000, normalPopulationCap: cap, creationBudget: budget);
        private static void SpawnWave(SpawnDirector director, double time, WorldRect bounds)
        { for (int i = 0; i < 10; i++) director.Tick(time + i * .02, bounds); }

        [Test]
        public void FirstWaveBeginsAtTwoMinutesAroundAllEightSectorsOutsideTheCamera()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            using var director = new SpawnDirector(rig.Run, WaveOnly());
            var view = new WorldRect(rig.Player.Position, 8, 4.5);
            director.Tick(119.98, view); Assert.That(director.SpawnedCount(UnitKind.Normal), Is.Zero);
            SpawnWave(director, 120, view);
            var enemies = rig.Run.Units.Where(unit => unit.Kind == UnitKind.Normal).ToArray();
            Assert.That(enemies.Length, Is.EqualTo(80));
            Assert.That(enemies.All(unit => !view.Contains(unit.Position, unit.BodyRadius) && rig.World.Query.IsCircleFree(unit.Position, unit.BodyRadius)), Is.True);
            var sectors = enemies.Select(unit =>
            {
                var d = view.Center.DisplacementTo(unit.Position); double angle = Math.Atan2(d.Y, d.X); if (angle < 0) angle += Math.PI * 2;
                return (int)(angle / (Math.PI / 4));
            }).Distinct();
            Assert.That(sectors.Count(), Is.EqualTo(8));
            director.Tick(121, view); Assert.That(director.SpawnedCount(UnitKind.Normal), Is.EqualTo(80));
        }

        [TestCase(135, 0, 100)]
        [TestCase(180, 0, 80)]
        [TestCase(135, 40, 80)]
        public void OnlyFastClearingWithFewSurvivorsRaisesTheNextWave(double killedAt, int survivors, int nextCount)
        {
            using var rig = RunTestRig.Create(enableAi: false);
            using var director = new SpawnDirector(rig.Run, WaveOnly());
            var view = new WorldRect(rig.Player.Position, 12, 8); SpawnWave(director, 120, view);
            Assert.That(director.SpawnedCount(UnitKind.Normal), Is.EqualTo(80));
            director.Tick(killedAt, view);
            foreach (var enemy in rig.Run.Units.Where(unit => unit.Kind == UnitKind.Normal).ToArray()) rig.Hit(enemy.Id, 10000);
            rig.Death.Flush();
            for (int i = 0; i < survivors; i++) rig.Spawn(UnitKind.Normal, new DVec2(100 + i, 100));
            SpawnWave(director, 240, view);
            Assert.That(director.SpawnedCount(UnitKind.Normal), Is.EqualTo(80 + nextCount));
            var next = rig.Run.Units.First(unit => unit.Kind == UnitKind.Normal && unit.Position.DistanceTo(view.Center) < 50);
            double expectedHealth = 10 * 1.8 * (nextCount == 100 ? 1.2 : 1);
            Assert.That(next.Definition.MaxHealth, Is.EqualTo(expectedHealth).Within(1e-8));
        }

        [Test]
        public void AliveRemovalDoesNotCountAsFastClearing()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            using var director = new SpawnDirector(rig.Run, WaveOnly());
            var view = new WorldRect(rig.Player.Position, 12, 8); SpawnWave(director, 120, view);
            foreach (var enemy in rig.Run.Units.Where(unit => unit.Kind == UnitKind.Normal).ToArray()) rig.World.Units.Remove(enemy.Id);
            SpawnWave(director, 240, view);
            Assert.That(director.SpawnedCount(UnitKind.Normal), Is.EqualTo(160));
        }

        [Test]
        public void WaveSharesTheNormalCapAndDoesNotAccumulateDebtWhenFull()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            using var director = new SpawnDirector(rig.Run, WaveOnly(20, 4));
            var view = new WorldRect(rig.Player.Position, 12, 8);
            director.Tick(120, view); Assert.That(director.SpawnedCount(UnitKind.Normal), Is.EqualTo(4));
            Assert.That(director.Pending.Sum(ticket => ticket.Remaining), Is.EqualTo(16));
            SpawnWave(director, 120.02, view); Assert.That(director.SpawnedCount(UnitKind.Normal), Is.EqualTo(20));
            director.Tick(240, view); Assert.That(director.Pending, Is.Empty);
            var enemy = rig.Run.Units.First(unit => unit.Kind == UnitKind.Normal); rig.World.Units.Remove(enemy.Id);
            director.Tick(241, view);
            Assert.That(director.SpawnedCount(UnitKind.Normal), Is.EqualTo(20));
            Assert.That(rig.Run.Units.Count(unit => unit.Kind == UnitKind.Normal), Is.EqualTo(19));
        }

        [TestCase(64, 2)] [TestCase(63, 1)]
        public void EscalationRequiresAtLeastEightyPercentFastKills(int kills, int expectedStage)
        {
            using var rig = RunTestRig.Create(enableAi: false);
            using var director = new SpawnDirector(rig.Run, WaveOnly());
            var view = new WorldRect(rig.Player.Position, 12, 8); SpawnWave(director, 120, view);
            director.Tick(130, view);
            foreach (var enemy in rig.Run.Units.Where(unit => unit.Kind == UnitKind.Normal).Take(kills).ToArray()) rig.Hit(enemy.Id, 10000);
            rig.Death.Flush(); SpawnWave(director, 240, view);
            Assert.That(director.SurroundWaveStage, Is.EqualTo(expectedStage));
        }

        [Test]
        public void BlockedWaveReservationsExpireAndAreNotBankedForOpenTerrain()
        {
            var terrain = new MutableTerrain(); using var rig = RunTestRig.Create(enableAi: false, terrain: terrain);
            using var director = new SpawnDirector(rig.Run, WaveOnly()); var view = new WorldRect(rig.Player.Position, 12, 8);
            director.Tick(120, view); Assert.That(director.Pending.Single().Remaining, Is.EqualTo(80));
            director.Tick(125, view); Assert.That(director.Pending, Is.Empty);
            terrain.Blocked = false; rig.World.ClearTerrainCache(); director.Tick(126, view);
            Assert.That(director.SpawnedCount(UnitKind.Normal), Is.Zero);
            SpawnWave(director, 240, view); Assert.That(director.SpawnedCount(UnitKind.Normal), Is.EqualTo(80));
            Assert.That(director.SurroundWaveStage, Is.EqualTo(1));
        }

        [Test]
        public void SeveralDeadlinesAreUniqueAndExistingMonstersKeepTheirHealthAndIdentity()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            using var director = new SpawnDirector(rig.Run, WaveOnly()); var view = new WorldRect(rig.Player.Position, 12, 8);
            SpawnWave(director, 120, view);
            var original = rig.Run.Units.Where(unit => unit.Kind == UnitKind.Normal).ToArray();
            double health = original[0].Health;
            rig.PlacePlayer(new DVec2(1000, 1000)); view = new WorldRect(rig.Player.Position, 12, 8);
            SpawnWave(director, 360, view);
            Assert.That(director.SurroundWaveNumber, Is.EqualTo(3));
            Assert.That(director.SurroundWaveSpawnedCount, Is.EqualTo(240));
            Assert.That(director.SpawnedCount(UnitKind.Boss), Is.EqualTo(1));
            Assert.That(original.All(unit => rig.World.Units.TryGet(unit.Id, out var current) && ReferenceEquals(unit, current)), Is.True);
            Assert.That(original[0].Health, Is.EqualTo(health));
        }

        [Test]
        public void RestartClearsWaveStageAndDeadlines()
        {
            using var rig = RunTestRig.Create(scheduledSpawns: true, enableAi: false, spawnSettings: WaveOnly());
            var old = rig.Simulation.Spawns; SpawnWave(old, 120, new WorldRect(rig.Player.Position, 12, 8));
            Assert.That(old.SurroundWaveNumber, Is.EqualTo(1));
            rig.Hit(rig.Player.Id, 10000); rig.Advance(.02); rig.RestartAsync().GetAwaiter().GetResult();
            Assert.That(rig.Simulation.Spawns.SurroundWaveNumber, Is.Zero);
            Assert.That(rig.Simulation.Spawns.SurroundWaveStage, Is.EqualTo(1));
            old.Tick(240, new WorldRect(rig.Player.Position, 12, 8));
            Assert.That(rig.Run.Units.Count, Is.EqualTo(1));
        }

        private sealed class MutableTerrain : IChunkGenerator
        {
            public bool Blocked = true;
            public ChunkData Generate(ChunkCoord coord)
            {
                var blocks = new bool[1024];
                if (Blocked) for (int i = 0; i < blocks.Length; i++) blocks[i] = true;
                return new ChunkData(coord, blocks, 1);
            }
        }
    }
}
