using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    internal sealed class NavigationGrid
    {
        internal static readonly (int x, int y)[] Directions = { (1, 0), (0, 1), (-1, 0), (0, -1), (1, 1), (-1, 1), (-1, -1), (1, -1) };
        private readonly WorldStore world;
        private readonly Dictionary<GridCell, bool> walkable = new Dictionary<GridCell, bool>();
        private readonly Dictionary<(GridCell, GridCell), bool> edges = new Dictionary<(GridCell, GridCell), bool>();
        private long revision;
        public double Radius { get; }
        public NavigationGrid(WorldStore world, double radius) { this.world = world; Radius = radius; revision = world.TerrainRevision; }
        private void Refresh()
        {
            if (revision == world.TerrainRevision) return;
            revision = world.TerrainRevision; walkable.Clear(); edges.Clear();
        }
        public bool IsWalkable(GridCell cell)
        {
            Refresh();
            if (!walkable.TryGetValue(cell, out bool value)) walkable[cell] = value = world.Query.IsCircleFree(cell.Center, Radius);
            return value;
        }
        public bool CanStep(GridCell from, GridCell to)
        {
            Refresh(); if (edges.TryGetValue((from, to), out bool result)) return result;
            result = IsWalkable(from) && IsWalkable(to);
            var delta = from.Center.DisplacementTo(to.Center);
            if (result && delta.X != 0 && delta.Y != 0)
                result = IsWalkable(from.Offset(Math.Sign(delta.X), 0)) && IsWalkable(from.Offset(0, Math.Sign(delta.Y)));
            if (result) result = !world.Query.SweepCircle(from.Center, delta, Radius).HasValue;
            edges[(from, to)] = result; edges[(to, from)] = result; return result;
        }
        public GridCell? Anchor(WorldPosition position)
        {
            if (!world.Query.IsCircleFree(position, Radius)) return null;
            var cell = GridCell.At(position); GridCell? best = null; double distance = double.PositiveInfinity;
            int range = Math.Max(1, (int)Math.Ceiling(Radius));
            for (int y = -range; y <= range; y++) for (int x = -range; x <= range; x++)
            {
                var candidate = cell.Offset(x, y); double d = position.DistanceTo(candidate.Center);
                if (d < distance && IsWalkable(candidate) && !world.Query.SweepCircle(position, position.DisplacementTo(candidate.Center), Radius).HasValue)
                { best = candidate; distance = d; }
            }
            return best;
        }
    }
}
