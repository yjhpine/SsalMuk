using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    public sealed class ForestChunkGenerator : IChunkGenerator
    {
        private readonly int seed;
        private readonly MapSettings settings;
        public ForestChunkGenerator(int seed, MapSettings settings)
        { this.seed = seed; this.settings = settings ?? throw new ArgumentNullException(nameof(settings)); }
        public ChunkData Generate(ChunkCoord coord)
        {
            var random = new SeededRandom(unchecked((int)StableHash.Combine(seed, coord.X, coord.Y, settings.GeneratorVersion, 71)));
            var reserved = new bool[1024]; var blocked = new bool[1024];
            var clearings = new List<ForestClearing>(); var scenery = new List<ForestScenery>();
            // Open edges and a six-cell cross keep all neighboring chunks reachable by the largest body.
            Reserve(0, 0, 32, 3); Reserve(0, 29, 32, 3); Reserve(0, 0, 3, 32); Reserve(29, 0, 3, 32);
            Reserve(13, 0, 6, 32); Reserve(0, 13, 32, 6); Reserve(12, 12, 8, 8);
            for (int q = 0; q < 4; q++)
            {
                int width = 5 + Draw(3), height = 5 + Draw(3);
                int x = (q % 2 == 0 ? 4 : 20) + Draw(3), y = (q < 2 ? 4 : 20) + Draw(3);
                clearings.Add(new ForestClearing(x, y, width, height)); Reserve(x, y, width, height);
                int cx = x + width / 2, cy = y + height / 2;
                Reserve(Math.Min(cx, 16) - 2, cy - 2, Math.Abs(cx - 16) + 4, 4);
                Reserve(14, Math.Min(cy, 16) - 2, 4, Math.Abs(cy - 16) + 4);
            }
            for (int y = 3; y <= 27; y += 4) for (int x = 3; x <= 27; x += 4)
            {
                int px = x + Draw(2), py = y + Draw(2);
                if (random.NextUnit() >= Math.Min(1, settings.ObstacleChance * 3)) continue;
                bool free = true;
                for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++)
                    if (reserved[(py + dy) * 32 + px + dx]) free = false;
                if (!free) continue;
                var kind = random.NextUnit() < .65 ? ForestPropKind.Tree : ForestPropKind.Rock;
                scenery.Add(new ForestScenery(kind, px, py, Draw(2)));
                for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++) blocked[(py + dy) * 32 + px + dx] = true;
            }
            // Grass is immutable decorative data, never collision or per-object simulation.
            for (int y = 0; y < 32; y += 2) for (int x = 0; x < 32; x += 2)
            {
                int px = x + Draw(2), py = y + Draw(2);
                if (!blocked[py * 32 + px] && random.NextUnit() < .72)
                    scenery.Add(new ForestScenery(ForestPropKind.Grass, px, py, Draw(2)));
            }
            return new ChunkData(coord, blocked, settings.GeneratorVersion, map: MapKind.Forest, scenery: scenery, clearings: clearings);
            int Draw(int count) => (int)(random.NextUnit() * count);
            void Reserve(int x, int y, int width, int height)
            { for (int cy = Math.Max(0, y); cy < Math.Min(32, y + height); cy++) for (int cx = Math.Max(0, x); cx < Math.Min(32, x + width); cx++) reserved[cy * 32 + cx] = true; }
        }
    }
}
