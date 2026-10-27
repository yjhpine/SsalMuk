using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    public sealed class LibraryChunkGenerator : IChunkGenerator
    {
        public const int InteriorBottom = 3, InteriorTop = 29;
        private readonly int seed;
        private readonly MapSettings settings;
        public LibraryChunkGenerator(int seed, MapSettings settings)
        { this.settings = settings ?? throw new ArgumentNullException(nameof(settings)); this.seed = seed; }
        public ChunkData Generate(ChunkCoord coord)
        {
            var blocked = new bool[1024]; var furniture = new List<FurniturePlacement>();
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                blocked[y * 32 + x] = coord.Y != 0 || y < InteriorBottom || y >= InteriorTop;
            if (coord.Y == 0)
            {
                var random = new SeededRandom(unchecked((int)StableHash.Combine(seed, coord.X, coord.Y, settings.GeneratorVersion, 89)));
                // Four rows of isolated furnishings leave the central aisle and edge lanes open.
                // Six-cell columns leave at least four cells between the widest footprints.
                foreach (int row in new[] { 6, 10, 21, 25 }) for (int column = 3; column <= 27; column += 6)
                {
                    if (random.NextUnit() >= Math.Min(.85, settings.ObstacleChance * 3.5)) continue;
                    var kind = (FurnitureKind)(int)(random.NextUnit() * 3);
                    var item = new FurniturePlacement(kind, column, row, kind == FurnitureKind.Chair ? 1 : 2, 1, random.NextUnit() < .5 ? 0 : 1);
                    furniture.Add(item);
                    for (int y = item.Y; y < item.Y + item.Height; y++) for (int x = item.X; x < item.X + item.Width; x++) blocked[y * 32 + x] = true;
                }
            }
            return new ChunkData(coord, blocked, settings.GeneratorVersion, furniture: furniture, map: MapKind.Library);
        }
    }
}
