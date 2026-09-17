using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    // Soft steering only: overlaps never cause position correction or move another unit.
    public sealed class CrowdSolver
    {
        private readonly MovementSettings settings;
        private readonly List<long> nearby = new List<long>(64);
        public CrowdSolver(MovementSettings settings = null) { this.settings = settings ?? new MovementSettings(); }

        internal DVec2 Steer(WorldStore world, UnitModel unit, DVec2 movement, double maximumRadius)
        {
            double length = movement.Length;
            if (unit.Kind == UnitKind.Player || unit.Kind == UnitKind.Air || length < 1e-9 || settings.CrowdAvoidanceStrength == 0) return movement;
            world.Query.QueryCircle(unit.Position, unit.BodyRadius + maximumRadius, nearby);
            var avoidance = DVec2.Zero; double weightSum = 0;
            foreach (long id in nearby)
            {
                if (id == unit.Id || !world.Units.TryGet(id, out var other) || !other.IsAlive || other.Kind == UnitKind.Air || other.Kind == UnitKind.Player) continue;
                var away = other.Position.DisplacementTo(unit.Position);
                double distance = away.Length, separation = unit.BodyRadius + other.BodyRadius;
                if (distance >= separation) continue;
                double weight = 1 - distance / separation;
                avoidance += (distance > 1e-9 ? away / distance : StableNormal(unit.Id, id)) * weight;
                weightSum += weight;
            }
            if (weightSum == 0) return movement;
            avoidance /= Math.Max(1, weightSum);
            var result = movement + avoidance * (length * settings.CrowdAvoidanceStrength);
            // Never add speed, teleport, or redirect attack knockback. Terrain remains authoritative.
            return result.Length > length ? result.Normalized * length : result;
        }

        private static DVec2 StableNormal(long a, long b)
        {
            ulong bits = StableHash.Combine(71, Math.Min(a, b), Math.Max(a, b), 1);
            double angle = (bits & 65535) * (Math.PI * 2 / 65536);
            var normal = new DVec2(Math.Cos(angle), Math.Sin(angle)); return a < b ? normal : -normal;
        }
    }
}
