using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    public sealed class ChunkGenerator : IChunkGenerator
    {
        private readonly int seed;
        public MapSettings Settings { get; }
        public ChunkGenerator(int seed, MapSettings settings) { this.seed = seed; Settings = settings ?? throw new ArgumentNullException(nameof(settings)); }

        public ChunkData Generate(ChunkCoord coord)
        {
            // Boundary points remain stable for the route planner; the whole boundary is now open.
            int[] openings = {
                Entrance(coord.X, coord.Y, 0), Entrance(checked(coord.X + 1), coord.Y, 0),
                Entrance(coord.X, coord.Y, 1), Entrance(coord.X, checked(coord.Y + 1), 1)
            };
            var cells = new bool[ChunkData.Size * ChunkData.Size];
            var furniture = new List<FurniturePlacement>();
            var random = new SeededRandom(unchecked((int)StableHash.Combine(seed, coord.X, coord.Y, Settings.GeneratorVersion)));
            // Isolated footprints leave continuous edge lanes and wide space between all slots.
            // The density setting is a placement probability, not a percentage of blocked cells.
            int margin = Math.Max(3, (int)Math.Ceiling(Settings.MaximumBodyRadius) + 1);
            int spacing = Math.Max(8, Settings.PassageWidth + 2);
            for (int y = margin; y + 2 <= ChunkData.Size - margin; y += spacing)
                for (int x = margin; x + 2 <= ChunkData.Size - margin; x += spacing)
                {
                    if (random.NextUnit() >= Settings.ObstacleChance) continue;
                    var kind = (FurnitureKind)(int)(random.NextUnit() * 3);
                    int width = kind == FurnitureKind.Chair ? 1 : 2;
                    if (x < 20 && x + width > 12 && y < 20 && y + 1 > 12) continue;
                    int variation = random.NextUnit() < .5 ? 0 : 1;
                    furniture.Add(new FurniturePlacement(kind, x, y, width, 1, variation));
                    for (int dx = 0; dx < width; dx++) cells[y * ChunkData.Size + x + dx] = true;
                }
            return new ChunkData(coord, cells, Settings.GeneratorVersion, openings, furniture: furniture);
        }

        private int Entrance(long x, long y, int axis) => 4 + (int)(StableHash.Combine(seed, x, y, Settings.GeneratorVersion, axis + 17) % (ulong)(24 - Settings.PassageWidth));
    }
}
