using System;
using System.Collections.Generic;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class WorldGenerationTests
    {
        [Test]
        public void ChunkDoesNotDependOnVisitOrderOrRewardConsumption()
        {
            var streams = new SeedStreams(1234);
            var generator = new ChunkGenerator(streams.MapSeed, MapSettings.TestDefaults());
            var coord = new ChunkCoord(-1, 2);
            string first = generator.Generate(coord).Fingerprint;
            generator.Generate(new ChunkCoord(50, -70));
            for (int i = 0; i < 1000; i++) streams.Reward.NextUnit();
            Assert.That(generator.Generate(coord).Fingerprint, Is.EqualTo(first));
            Assert.That(new ChunkGenerator(streams.MapSeed, MapSettings.TestDefaults()).Generate(coord).Fingerprint, Is.EqualTo(first));
            Assert.That(generator.Generate(new ChunkCoord(2, -1)).Fingerprint, Is.Not.EqualTo(first));
        }

        [TestCase(0, 0)] [TestCase(-1, -1)] [TestCase(-50, 71)] [TestCase(420, -900)]
        public void AdjacentOpeningsAgreeAndBossCanCrossThem(long x, long y)
        {
            var generator = new ChunkGenerator(875, MapSettings.TestDefaults(0.65));
            var a = generator.Generate(new ChunkCoord(x, y));
            var east = generator.Generate(new ChunkCoord(x + 1, y));
            var north = generator.Generate(new ChunkCoord(x, y + 1));
            for (int i = 0; i < ChunkData.Size; i++)
            {
                Assert.That(a.IsBlocked(31, i), Is.EqualTo(east.IsBlocked(0, i)));
                Assert.That(a.IsBlocked(i, 31), Is.EqualTo(north.IsBlocked(i, 0)));
            }
            using var world = new WorldStore(new UnitRegistry(Guid.NewGuid()), generator);
            var start = new WorldPosition(a.Coord, new DVec2(16.5, 16.5));
            var target = new WorldPosition(east.Coord, new DVec2(16.5, 16.5));
            Assert.That(CanReach(world, start, target, 0.75, a.Coord, east.Coord), Is.True);
            Assert.That(CanReach(world, start, new WorldPosition(north.Coord, new DVec2(16.5, 16.5)), 0.75, a.Coord, north.Coord), Is.True);
        }

        [Test]
        public void EveryFloorCellBelongsToTheCentralConnectedRegion()
        {
            var generator = new ChunkGenerator(42, MapSettings.TestDefaults(0.4));
            for (int c = -8; c <= 8; c++)
            {
                ChunkData chunk = generator.Generate(new ChunkCoord(c, -c));
                var seen = new HashSet<int>();
                var pending = new Queue<int>();
                pending.Enqueue(16 * 32 + 16); seen.Add(16 * 32 + 16);
                while (pending.Count > 0)
                {
                    int cell = pending.Dequeue(), x = cell % 32, y = cell / 32;
                    foreach (var d in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        int nx = x + d.Item1, ny = y + d.Item2, next = ny * 32 + nx;
                        if (nx >= 0 && nx < 32 && ny >= 0 && ny < 32 && !chunk.IsBlocked(nx, ny) && seen.Add(next)) pending.Enqueue(next);
                    }
                }
                for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                    if (!chunk.IsBlocked(x, y)) Assert.That(seen.Contains(y * 32 + x), Is.True);
            }
        }

        [Test]
        public void ZeroObstaclePresetIsOpenAndInvalidPassageSettingsAreRejected()
        {
            var chunk = new ChunkGenerator(9, MapSettings.TestDefaults(0)).Generate(default);
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) Assert.That(chunk.IsBlocked(x, y), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => new MapSettings(0.2, 1, 4, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => MapSettings.TestDefaults(double.NaN));
        }

        [Test]
        public void RandomStreamHasStableKnownValuesAndIndependentChannels()
        {
            var random = new SeededRandom(1);
            Assert.That(random.NextUnit(), Is.EqualTo(270369 / 4294967296.0));
            Assert.That(random.NextUnit(), Is.EqualTo(67634689 / 4294967296.0));
            Assert.That(random.NextUnit(), Is.EqualTo(2647435461 / 4294967296.0));
            Assert.That(new SeededRandom(0).NextUnit(), Is.GreaterThan(0).And.LessThan(1));
            var a = new SeedStreams(1); var b = new SeedStreams(1);
            for (int i = 0; i < 30; i++) a.Reward.NextUnit();
            Assert.That(a.Spawn.NextUnit(), Is.EqualTo(b.Spawn.NextUnit()));
            Assert.That(a.MapSeed, Is.Not.EqualTo(a.SpawnSeed).And.Not.EqualTo(a.RewardSeed));
        }

        private static bool CanReach(WorldStore world, WorldPosition from, WorldPosition to, double radius, ChunkCoord a, ChunkCoord b)
        {
            var start = GridCell.At(from); var target = GridCell.At(to);
            var seen = new HashSet<GridCell> { start }; var queue = new Queue<GridCell>(); queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue(); if (cell.Equals(target)) return true;
                foreach (var d in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var next = cell.Offset(d.Item1, d.Item2);
                    if ((next.Chunk.Equals(a) || next.Chunk.Equals(b)) && seen.Add(next) &&
                        world.Query.IsCircleFree(next.Center, radius) && !world.Query.SweepCircle(cell.Center, cell.Center.DisplacementTo(next.Center), radius).HasValue)
                        queue.Enqueue(next);
                }
            }
            return false;
        }
    }
}
