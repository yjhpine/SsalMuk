using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    public sealed class WorldQuery : IWorldQuery
    {
        private readonly WorldStore world;
        private readonly SpatialIndex units, experience;
        internal WorldQuery(WorldStore world, SpatialIndex units, SpatialIndex experience)
        { this.world = world; this.units = units; this.experience = experience; }
        public bool TryGetUnit(long id, out UnitModel unit) => world.Units.TryGet(id, out unit);
        public long? FindNearestEnemy(WorldPosition position) => units.FindNearest(position,
            id => world.Units.TryGet(id, out var unit) && unit.IsAlive && unit.Kind != UnitKind.Player);
        public IReadOnlyList<long> QueryCircle(WorldPosition position, double radius) => units.QueryCircle(position, radius);
        public IReadOnlyList<long> QueryExperienceCircle(WorldPosition position, double radius) => experience.QueryCircle(position, radius);
        public bool IsCircleFree(WorldPosition position, double radius)
        {
            SpatialIndex.RequireRadius(radius);
            foreach (var cell in CircleSweep.CandidateCells(position, DVec2.Zero, radius))
            {
                if (!world.IsBlocked(cell)) continue;
                var min = position.DisplacementTo(new WorldPosition(cell.Chunk, new DVec2(cell.X, cell.Y)));
                if (CircleSweep.OverlapsBox(DVec2.Zero, radius, min, min + new DVec2(1, 1))) return false;
            }
            return true;
        }
        public SweepHit? SweepCircle(WorldPosition position, DVec2 displacement, double radius)
        {
            SpatialIndex.RequireRadius(radius);
            if (!IsCircleFree(position, radius)) throw new InvalidOperationException("Movement starts inside an obstacle. A safe starting position is required.");
            if (displacement == DVec2.Zero) return null;
            SweepHit? best = null;
            foreach (var cell in CircleSweep.CandidateCells(position, displacement, radius))
            {
                if (!world.IsBlocked(cell)) continue;
                var min = position.DisplacementTo(new WorldPosition(cell.Chunk, new DVec2(cell.X, cell.Y)));
                var hit = CircleSweep.AgainstBox(DVec2.Zero, displacement, radius, min, min + new DVec2(1, 1));
                if (hit.HasValue && (!best.HasValue || hit.Value.Fraction < best.Value.Fraction))
                    best = new SweepHit(hit.Value.Fraction, hit.Value.Normal, position.Offset(displacement * hit.Value.Fraction));
            }
            return best;
        }
    }
}
