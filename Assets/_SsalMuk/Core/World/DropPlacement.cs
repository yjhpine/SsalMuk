using System;

namespace SsalMuk.Core
{
    public static class DropPlacement
    {
        // Grid floor centers are deterministic across chunks. Never discard or merge the reward.
        public static WorldPosition NearestFreeFloor(WorldStore world, WorldPosition origin, double playerRadius)
        {
            if (world.Query.IsCircleFree(origin, playerRadius)) return origin;
            var center = GridCell.At(origin); WorldPosition best = origin; double bestDistance = double.PositiveInfinity;
            Consider(center.Center);
            for (int ring = 1; ; ring = checked(ring + 1))
            {
                for (int x = -ring; x <= ring; x++)
                {
                    Consider(center.Offset(x, -ring).Center); Consider(center.Offset(x, ring).Center);
                }
                for (int y = -ring + 1; y < ring; y++)
                {
                    Consider(center.Offset(-ring, y).Center); Consider(center.Offset(ring, y).Center);
                }
                if (bestDistance <= ring + 0.5) return best;
            }
            void Consider(WorldPosition candidate)
            {
                double distance = origin.DistanceTo(candidate);
                if (distance < bestDistance && world.Query.IsCircleFree(candidate, playerRadius)) { best = candidate; bestDistance = distance; }
            }
        }
    }
}
