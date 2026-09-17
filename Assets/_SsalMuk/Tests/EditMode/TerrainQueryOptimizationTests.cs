using System;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class TerrainQueryOptimizationTests
    {
        [TestCase(0, 0, 5, 4)] [TestCase(-1, -1, 31, 31)] [TestCase(1000000000, -1000000000, 0, 0)]
        public void ShortAndLongSweepsMatchExactBoxAcrossChunkBoundaries(long cx, long cy, int x, int y)
        {
            var coord = new ChunkCoord(cx, cy);
            using var world = new WorldStore(new UnitRegistry(Guid.NewGuid()), new CircleSweepTests.SingleWall(coord, x, y));
            var box = new WorldPosition(coord, new DVec2(x, y));
            var random = new Random(716);
            for (int i = 0; i < 180; i++)
            {
                var offset = new DVec2(random.NextDouble() * 14 - 7, random.NextDouble() * 14 - 7);
                var start = box.Offset(offset);
                double radius = i % 4 == 0 ? 3.5 : i % 3 == 0 ? 0 : .26;
                var delta = new DVec2(random.NextDouble() * 2 - 1, random.NextDouble() * 2 - 1) * (i % 2 == 0 ? 5 : 40);
                var min = start.DisplacementTo(box); var max = min + new DVec2(1, 1);
                bool free = !CircleSweep.OverlapsBox(DVec2.Zero, radius, min, max);
                Assert.That(world.Query.IsCircleFree(start, radius), Is.EqualTo(free), "Overlap sample " + i);
                if (!free)
                {
                    Assert.Throws<InvalidOperationException>(() => world.Query.SweepCircle(start, delta, radius));
                    Assert.Throws<InvalidOperationException>(() => world.Query.SweepCircle(start, DVec2.Zero, radius));
                    continue;
                }
                var expected = CircleSweep.AgainstBox(DVec2.Zero, delta, radius, min, max);
                var actual = world.Query.SweepCircle(start, delta, radius);
                Assert.That(actual.HasValue, Is.EqualTo(expected.HasValue), "Sweep sample " + i);
                if (!expected.HasValue) continue;
                Assert.That(actual.Value.Fraction, Is.EqualTo(expected.Value.Fraction).Within(1e-9));
                Assert.That(actual.Value.Normal.X, Is.EqualTo(expected.Value.Normal.X).Within(1e-9));
                Assert.That(actual.Value.Normal.Y, Is.EqualTo(expected.Value.Normal.Y).Within(1e-9));
            }
        }

        [Test]
        public void EmptyChunkMovementKeepsFullDisplacementAndRejectsInvalidRadius()
        {
            using var world = new WorldStore(new UnitRegistry(Guid.NewGuid()), new ChunkGenerator(3, new MapSettings(0)));
            var start = WorldPosition.FromLocal(new DVec2(-.1, -.1));
            var delta = new DVec2(6.4, -3.2);
            Assert.That(CircleSweep.MoveAndSlide(world.Query, start, delta, .9), Is.EqualTo(start.Offset(delta)));
            Assert.Throws<ArgumentOutOfRangeException>(() => CircleSweep.MoveAndSlide(world.Query, start, delta, double.NaN));
        }
    }
}
