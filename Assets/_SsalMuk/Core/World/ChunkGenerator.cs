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
            int[] openings = {
                Entrance(coord.X, coord.Y, 0), Entrance(checked(coord.X + 1), coord.Y, 0),
                Entrance(coord.X, coord.Y, 1), Entrance(coord.X, checked(coord.Y + 1), 1)
            };
            var cells = new bool[32 * 32];
            if (Settings.ObstacleChance == 0) return new ChunkData(coord, cells, Settings.GeneratorVersion, openings);
            var reserved = new bool[cells.Length];
            Reserve(reserved, 12, 12, 20, 20);
            int width = Settings.PassageWidth;
            for (int side = 0; side < 4; side++)
            {
                int p = openings[side];
                if (side < 2)
                {
                    Reserve(reserved, side == 0 ? 0 : 14, p, side == 0 ? 18 : 32, p + width);
                    Reserve(reserved, 14, Math.Min(p, 14), 14 + width, Math.Max(p, 14) + width);
                }
                else
                {
                    Reserve(reserved, p, side == 2 ? 0 : 14, p + width, side == 2 ? 18 : 32);
                    Reserve(reserved, Math.Min(p, 14), 14, Math.Max(p, 14) + width, 14 + width);
                }
            }
            var random = new SeededRandom(unchecked((int)StableHash.Combine(seed, coord.X, coord.Y, Settings.GeneratorVersion)));
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                cells[y * 32 + x] = !reserved[y * 32 + x] && (random.NextUnit() < Settings.ObstacleChance || x == 0 || x == 31 || y == 0 || y == 31);

            bool fallback = !AllFloorConnected(cells) || !EntrancesHaveClearance(cells, openings);
            if (fallback)
            {
                // A deterministic open interior with the same boundary portals is the safe layout.
                for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                    cells[y * 32 + x] = !reserved[y * 32 + x] && (x == 0 || x == 31 || y == 0 || y == 31);
                if (!AllFloorConnected(cells) || !EntrancesHaveClearance(cells, openings)) throw new InvalidOperationException("Map settings cannot produce connected, body-safe passages.");
            }
            return new ChunkData(coord, cells, Settings.GeneratorVersion, openings, fallback);
        }

        private int Entrance(long x, long y, int axis) => 4 + (int)(StableHash.Combine(seed, x, y, Settings.GeneratorVersion, axis + 17) % (ulong)(24 - Settings.PassageWidth));

        private static void Reserve(bool[] cells, int x0, int y0, int x1, int y1)
        { for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) cells[y * 32 + x] = true; }

        private static bool AllFloorConnected(bool[] cells)
        {
            var seen = Flood(cells, 16, 16, 0);
            for (int i = 0; i < cells.Length; i++) if (!cells[i] && !seen[i]) return false;
            return true;
        }

        private bool EntrancesHaveClearance(bool[] cells, int[] openings)
        {
            var seen = Flood(cells, 16, 16, Settings.MaximumBodyRadius);
            int offset = Settings.PassageWidth / 2;
            return seen[(openings[0] + offset) * 32] && seen[(openings[1] + offset) * 32 + 31] &&
                seen[openings[2] + offset] && seen[31 * 32 + openings[3] + offset];
        }

        private static bool[] Flood(bool[] blocked, int sx, int sy, double radius)
        {
            var seen = new bool[blocked.Length]; var queue = new Queue<int>();
            var free = new bool[blocked.Length];
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                bool clear = !blocked[y * 32 + x];
                int range = (int)Math.Ceiling(radius);
                for (int dy = -range; clear && dy <= range; dy++) for (int dx = -range; clear && dx <= range; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || nx >= 32 || ny < 0 || ny >= 32 || !blocked[ny * 32 + nx]) continue;
                    double vx = Math.Max(0, Math.Abs(dx) - 0.5), vy = Math.Max(0, Math.Abs(dy) - 0.5);
                    if (vx * vx + vy * vy < radius * radius) clear = false;
                }
                free[y * 32 + x] = clear;
            }
            if (!free[sy * 32 + sx]) return seen;
            queue.Enqueue(sy * 32 + sx); seen[sy * 32 + sx] = true;
            while (queue.Count > 0)
            {
                int cell = queue.Dequeue(), x = cell % 32, y = cell / 32;
                if (x > 0) Visit(cell - 1); if (x < 31) Visit(cell + 1);
                if (y > 0) Visit(cell - 32); if (y < 31) Visit(cell + 32);
            }
            return seen;
            void Visit(int cell) { if (free[cell] && !seen[cell]) { seen[cell] = true; queue.Enqueue(cell); } }
        }
    }
}
