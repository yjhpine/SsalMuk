using System.Collections.Generic;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class OpenFurnitureWorldTests
    {
        [TestCase(0, 0)] [TestCase(-5, 3)] [TestCase(22, -31)]
        public void GeneratedChunksHaveNoPerimeterWalls(long x, long y)
        {
            var chunk = new ChunkGenerator(875, new MapSettings(.8, maximumBodyRadius: .9)).Generate(new ChunkCoord(x, y));
            for (int i = 0; i < 32; i++)
            {
                Assert.That(chunk.IsBlocked(0, i), Is.False);
                Assert.That(chunk.IsBlocked(31, i), Is.False);
                Assert.That(chunk.IsBlocked(i, 0), Is.False);
                Assert.That(chunk.IsBlocked(i, 31), Is.False);
            }
        }

        [Test]
        public void EveryBlockedCellBelongsToOneReproducibleFurnitureFootprint()
        {
            var generator = new ChunkGenerator(144, new MapSettings(1));
            var first = generator.Generate(new ChunkCoord(-4, 7));
            var again = generator.Generate(first.Coord);
            Assert.That(first.Fingerprint, Is.EqualTo(again.Fingerprint));
            Assert.That(first.Furniture, Is.EqualTo(again.Furniture));
            var occupied = new HashSet<int>();
            foreach (var item in first.Furniture)
                for (int y = item.Y; y < item.Y + item.Height; y++) for (int x = item.X; x < item.X + item.Width; x++)
                    Assert.That(occupied.Add(y * 32 + x), Is.True, "Footprints must not overlap.");
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                Assert.That(first.IsBlocked(x, y), Is.EqualTo(occupied.Contains(y * 32 + x)));
        }

        [Test]
        public void FurnitureFootprintsAreSmallSeparatedAndLeaveSpawnSpace()
        {
            var chunk = new ChunkGenerator(71, new MapSettings(1)).Generate(default);
            var visited = new HashSet<int>(); int groups = 0, blocked = 0;
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                if (!chunk.IsBlocked(x, y)) continue;
                blocked++;
                Assert.That(x >= 12 && x < 20 && y >= 12 && y < 20, Is.False, "Initial spawn space must stay clear.");
                int first = y * 32 + x;
                if (!visited.Add(first)) continue;
                groups++; var queue = new Queue<int>(); queue.Enqueue(first); int size = 0;
                while (queue.Count > 0)
                {
                    int cell = queue.Dequeue(); size++;
                    foreach (var d in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        int nx = cell % 32 + d.Item1, ny = cell / 32 + d.Item2;
                        if (nx >= 0 && nx < 32 && ny >= 0 && ny < 32 && chunk.IsBlocked(nx, ny) && visited.Add(ny * 32 + nx)) queue.Enqueue(ny * 32 + nx);
                    }
                }
                Assert.That(size, Is.LessThanOrEqualTo(4), "Furniture must not form a connected wall.");
            }
            Assert.That(groups, Is.GreaterThan(2));
            Assert.That(blocked, Is.LessThan(64));
        }
    }
}
