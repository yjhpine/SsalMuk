using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    public sealed class LibraryChunkGenerator : IChunkGenerator
    {
        public const int InteriorBottom = 3, InteriorTop = 29;
        private readonly ChunkGenerator furnitureGenerator;
        private readonly MapSettings settings;
        public LibraryChunkGenerator(int seed, MapSettings settings)
        { this.settings = settings ?? throw new ArgumentNullException(nameof(settings)); furnitureGenerator = new ChunkGenerator(seed, settings); }
        public ChunkData Generate(ChunkCoord coord)
        {
            var blocked = new bool[1024]; var furniture = new List<FurniturePlacement>();
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                blocked[y * 32 + x] = coord.Y != 0 || y < InteriorBottom || y >= InteriorTop;
            if (coord.Y == 0)
            {
                foreach (var item in furnitureGenerator.Generate(coord).Furniture)
                {
                    // Keep the main horizontal corridor open even for the boss.
                    if (item.Y < InteriorBottom + 2 || item.Y + item.Height > InteriorTop - 2 || item.Y >= 12 && item.Y < 20) continue;
                    furniture.Add(item);
                    for (int y = item.Y; y < item.Y + item.Height; y++) for (int x = item.X; x < item.X + item.Width; x++) blocked[y * 32 + x] = true;
                }
            }
            return new ChunkData(coord, blocked, settings.GeneratorVersion, furniture: furniture, map: MapKind.Library);
        }
    }
}
