using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    public sealed class ChunkRoutePlanner
    {
        private readonly WorldStore world;
        public ChunkRoutePlanner(WorldStore world) { this.world = world ?? throw new ArgumentNullException(nameof(world)); }
        internal Search Request(ChunkCoord from, ChunkCoord to, double radius) => new Search(world, from, to, radius);

        internal sealed class Search
        {
            private readonly WorldStore world;
            private readonly ChunkCoord from, target;
            private readonly double radius;
            private readonly SearchQueue<ChunkCoord> open = new SearchQueue<ChunkCoord>();
            private readonly Dictionary<ChunkCoord, double> costs = new Dictionary<ChunkCoord, double>();
            private readonly Dictionary<ChunkCoord, ChunkCoord> parents = new Dictionary<ChunkCoord, ChunkCoord>();
            private readonly HashSet<ChunkCoord> closed = new HashSet<ChunkCoord>();
            public PathStatus Status { get; private set; } = PathStatus.Pending;
            public List<ChunkCoord> Route { get; } = new List<ChunkCoord>();
            public Search(WorldStore world, ChunkCoord from, ChunkCoord target, double radius)
            {
                this.world = world; this.from = from; this.target = target; this.radius = radius;
                costs[from] = 0; open.Push(from, Heuristic(from));
                if (from.Equals(target)) { Route.Add(from); Status = PathStatus.Ready; }
            }
            private double Heuristic(ChunkCoord cell) => (double)(Math.Abs((decimal)cell.X - target.X) + Math.Abs((decimal)cell.Y - target.Y));
            public int Advance(int budget)
            {
                int used = 0;
                while (Status == PathStatus.Pending && open.Count > 0 && used < budget)
                {
                    used++; var cell = open.Pop(); if (!closed.Add(cell)) continue;
                    if (cell.Equals(target))
                    {
                        while (!cell.Equals(from)) { Route.Add(cell); cell = parents[cell]; }
                        Route.Add(from); Route.Reverse(); Status = PathStatus.Ready; break;
                    }
                    for (int side = 0; side < 4; side++)
                    {
                        long dx = side == 0 ? -1 : side == 1 ? 1 : 0;
                        long dy = side == 2 ? -1 : side == 3 ? 1 : 0;
                        var next = new ChunkCoord(checked(cell.X + dx), checked(cell.Y + dy));
                        if (closed.Contains(next) || !PortalIsOpen(cell, (ChunkSide)side)) continue;
                        double cost = costs[cell] + 1;
                        if (costs.TryGetValue(next, out double existing) && existing <= cost) continue;
                        costs[next] = cost; parents[next] = cell; double h = Heuristic(next); open.Push(next, cost + h, h);
                    }
                }
                if (Status == PathStatus.Pending && open.Count == 0) Status = PathStatus.NoPath;
                return used;
            }
            private bool PortalIsOpen(ChunkCoord coord, ChunkSide side)
            {
                int opening = world.GetChunk(coord).OpeningStart(side);
                // Verify the actual boundary cells; custom maps can have a closed advertised portal.
                for (int i = 0; i < 4; i++)
                {
                    double p = opening + i + 0.5;
                    DVec2 start, delta;
                    if (side == ChunkSide.West) { start = new DVec2(0.5, p); delta = new DVec2(-1, 0); }
                    else if (side == ChunkSide.East) { start = new DVec2(31.5, p); delta = new DVec2(1, 0); }
                    else if (side == ChunkSide.South) { start = new DVec2(p, 0.5); delta = new DVec2(0, -1); }
                    else { start = new DVec2(p, 31.5); delta = new DVec2(0, 1); }
                    var a = new WorldPosition(coord, start); var b = a.Offset(delta);
                    if (world.Query.IsCircleFree(a, radius) && world.Query.IsCircleFree(b, radius) && !world.Query.SweepCircle(a, delta, radius).HasValue) return true;
                }
                return false;
            }
        }
    }
}
