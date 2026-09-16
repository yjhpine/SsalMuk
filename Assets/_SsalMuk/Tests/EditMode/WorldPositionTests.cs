using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class WorldPositionTests
    {
        [TestCase(-0.1, -1, 31.9)] [TestCase(-32, -1, 0)] [TestCase(32, 1, 0)]
        public void LocalCoordinatesNormalizeAcrossChunkBoundaries(double x, long chunkX, double localX)
        {
            var position = WorldPosition.FromLocal(new DVec2(x, 0));
            Assert.That(position.Chunk.X, Is.EqualTo(chunkX));
            Assert.That(position.Local.X, Is.EqualTo(localX).Within(1e-10));
        }

        [Test]
        public void SmallMovementRemainsPreciseFarFromTheOrigin()
        {
            var start = new WorldPosition(new ChunkCoord(9007199254740993L, -9007199254740993L), new DVec2(31.9, 8));
            var moved = start.Offset(new DVec2(0.2, 0));
            Assert.That(moved.Chunk.X, Is.EqualTo(start.Chunk.X + 1));
            Assert.That(moved.Local.X, Is.EqualTo(0.1).Within(1e-10));
            Assert.That(start.DistanceTo(moved), Is.EqualTo(0.2).Within(1e-10));
        }

        [Test]
        public void ZeroDirectionStaysZeroAndOtherDirectionsNormalize()
        {
            Assert.That(DVec2.Zero.Normalized, Is.EqualTo(DVec2.Zero));
            Assert.That(new DVec2(3, 4).Normalized.Length, Is.EqualTo(1).Within(1e-12));
        }
    }
}
