using System;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class CircleSweepTests
    {
        [Test]
        public void FastMotionFindsFirstFaceAndDoesNotMutateThePosition()
        {
            using var world = CreateWall();
            var start = WorldPosition.FromLocal(new DVec2(0.5, 4.5));
            var hit = world.Query.SweepCircle(start, new DVec2(20, 0), 0.25);
            Assert.That(hit.HasValue, Is.True);
            Assert.That(hit.Value.Fraction, Is.EqualTo(4.25 / 20).Within(1e-9));
            Assert.That(hit.Value.Normal, Is.EqualTo(new DVec2(-1, 0)));
            Assert.That(hit.Value.Position.Local.X, Is.EqualTo(4.75).Within(1e-9));
            Assert.That(start.Local.X, Is.EqualTo(0.5));
        }

        [Test]
        public void RoundedCornerUsesCircleGeometryAndAllowsTangentialMotion()
        {
            var min = new DVec2(0, 0); var max = new DVec2(1, 1);
            var hit = CircleSweep.AgainstBox(new DVec2(-1, -1), new DVec2(2, 2), 0.5, min, max);
            Assert.That(hit.HasValue, Is.True);
            Assert.That(hit.Value.Fraction, Is.EqualTo((1 - 0.5 / Math.Sqrt(2)) / 2).Within(1e-9));
            Assert.That(hit.Value.Normal.X, Is.EqualTo(-1 / Math.Sqrt(2)).Within(1e-9));
            Assert.That(CircleSweep.AgainstBox(new DVec2(-0.5, 0.5), new DVec2(0, 0.4), 0.5, min, max), Is.Null);
            Assert.That(CircleSweep.AgainstBox(new DVec2(-0.5, 0.5), new DVec2(-1, 0), 0.5, min, max), Is.Null);
        }

        [Test]
        public void StartsInsideObstacleAreRejectedAndNegativeBoundaryWorks()
        {
            using var world = CreateWall();
            Assert.Throws<InvalidOperationException>(() => world.Query.SweepCircle(WorldPosition.FromLocal(new DVec2(5.5, 4.5)), new DVec2(1, 0), 0.25));
            Assert.That(world.Query.IsCircleFree(WorldPosition.FromLocal(new DVec2(5.1, 4.5)), 0.25), Is.False);
            using var negative = new WorldStore(new UnitRegistry(Guid.NewGuid()), new SingleWall(new ChunkCoord(-1, -1), 31, 31));
            var hit = negative.Query.SweepCircle(WorldPosition.FromLocal(new DVec2(-3, -0.5)), new DVec2(5, 0), 0.25);
            Assert.That(hit.Value.Fraction, Is.EqualTo(1.75 / 5).Within(1e-9));
        }

        internal static WorldStore CreateWall() => new WorldStore(new UnitRegistry(Guid.NewGuid()), new SingleWall(default, 5, 4));

        internal sealed class SingleWall : IChunkGenerator
        {
            private readonly ChunkCoord coord; private readonly int x, y;
            public SingleWall(ChunkCoord coord, int x, int y) { this.coord = coord; this.x = x; this.y = y; }
            public ChunkData Generate(ChunkCoord requested)
            {
                var cells = new bool[32 * 32]; if (requested.Equals(coord)) cells[y * 32 + x] = true;
                return new ChunkData(requested, cells, 1);
            }
        }
    }
}
