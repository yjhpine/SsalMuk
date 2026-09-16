using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    public sealed class CrowdSolver
    {
        private readonly MovementSettings settings;
        private readonly Dictionary<long, int> tangentSides = new Dictionary<long, int>();
        public CrowdSolver(MovementSettings settings = null) { this.settings = settings ?? new MovementSettings(); }

        internal DVec2 Constrain(WorldStore world, UnitModel unit, DVec2 movement, double maximumRadius)
        {
            if (movement.Length < 1e-9) return movement;
            var original = unit.Position;
            var nearby = world.Query.QueryCircle(original, unit.BodyRadius + maximumRadius + movement.Length);
            foreach (long id in nearby)
            {
                if (id == unit.Id || !world.Units.TryGet(id, out var other) || !other.IsAlive || other.Kind == UnitKind.Air) continue;
                var relative = other.Position.DisplacementTo(original);
                double radius = unit.BodyRadius + other.BodyRadius;
                double a = DVec2.Dot(movement, movement), b = DVec2.Dot(relative, movement), c = DVec2.Dot(relative, relative) - radius * radius;
                if (a < 1e-14 || (c > 0 && b >= 0)) continue;
                double discriminant = b * b - a * c;
                if (discriminant < 0) continue;
                double fraction = c <= 0 ? 0 : (-b - Math.Sqrt(discriminant)) / a;
                if (fraction < 0 || fraction > 1) continue;
                var normal = (relative + movement * fraction).Normalized;
                if (normal == DVec2.Zero) normal = StableNormal(unit.Id, other.Id);
                var approach = movement * Math.Max(0, fraction - 1e-6);
                var remainder = movement * (1 - fraction);
                double inward = DVec2.Dot(remainder, normal);
                if (inward >= 0) continue;
                remainder -= normal * inward;
                if (remainder.Length < 1e-7)
                {
                    double distance = movement.Length * (1 - fraction);
                    var tangent = new DVec2(-normal.Y, normal.X);
                    if (!tangentSides.TryGetValue(unit.Id, out int side)) side = (unit.Id & 1) == 0 ? 1 : -1;
                    var contact = original.Offset(approach);
                    double preferred = ClearanceCost(contact, tangent * (distance * side));
                    double alternate = ClearanceCost(contact, tangent * (-distance * side));
                    if (alternate + 1e-6 < preferred) side = -side;
                    tangentSides[unit.Id] = side;
                    if (double.IsPositiveInfinity(Math.Min(preferred, alternate))) remainder = DVec2.Zero;
                    else remainder = tangent * (distance * side);
                }
                movement = approach + remainder;
            }
            return movement;

            double ClearanceCost(WorldPosition contact, DVec2 displacement)
            {
                if (!world.Query.IsCircleFree(contact, unit.BodyRadius) || world.Query.SweepCircle(contact, displacement, unit.BodyRadius).HasValue) return double.PositiveInfinity;
                var target = contact.Offset(displacement); double cost = 0;
                foreach (long id in nearby)
                {
                    if (id == unit.Id || !world.Units.TryGet(id, out var body) || !body.IsAlive || body.Kind == UnitKind.Air) continue;
                    cost += Math.Max(0, unit.BodyRadius + body.BodyRadius - target.DistanceTo(body.Position));
                }
                return cost;
            }
        }

        public void Resolve(WorldStore world, double dt)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (dt <= 0 || double.IsNaN(dt) || double.IsInfinity(dt)) throw new ArgumentOutOfRangeException(nameof(dt));
            var ground = new List<UnitModel>(); double largest = 0;
            foreach (var unit in world.Units.Units)
                if (unit.IsAlive && unit.Kind != UnitKind.Air) { ground.Add(unit); largest = Math.Max(largest, unit.BodyRadius); }
            ground.Sort((a, b) => a.Id.CompareTo(b.Id));
            for (int iteration = 0; iteration < settings.CrowdIterations; iteration++)
            {
                bool changed = false;
                foreach (var a in ground)
                {
                    foreach (long id in world.Query.QueryCircle(a.Position, a.BodyRadius + largest))
                    {
                        if (id <= a.Id || !world.Units.TryGet(id, out var b) || !b.IsAlive || b.Kind == UnitKind.Air) continue;
                        var delta = a.Position.DisplacementTo(b.Position); double distance = delta.Length;
                        double overlap = a.BodyRadius + b.BodyRadius - distance;
                        if (overlap <= 1e-5) continue;
                        var normal = distance < 1e-9 ? StableNormal(a.Id, b.Id) : delta / distance;
                        var correction = normal * (overlap * 0.5);
                        var nextA = CircleSweep.MoveAndSlide(world.Query, a.Position, -correction, a.BodyRadius, settings.SlideContacts);
                        var nextB = CircleSweep.MoveAndSlide(world.Query, b.Position, correction, b.BodyRadius, settings.SlideContacts);
                        changed |= nextA != a.Position || nextB != b.Position;
                        world.MoveUnit(a.Id, nextA); world.MoveUnit(b.Id, nextB);
                    }
                }
                if (!changed) break;
            }
        }
        internal void Forget(long id) => tangentSides.Remove(id);
        private static DVec2 StableNormal(long a, long b)
        {
            ulong bits = StableHash.Combine(71, Math.Min(a, b), Math.Max(a, b), 1);
            double angle = (bits & 65535) * (Math.PI * 2 / 65536);
            var normal = new DVec2(Math.Cos(angle), Math.Sin(angle)); return a < b ? normal : -normal;
        }
    }
}
