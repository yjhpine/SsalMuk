using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    // Bounded contact and separation passes over snapshots. Never changes an enemy's AI.
    public sealed class CrowdSolver
    {
        private const int SamplesPerCell = 8;
        private const double NormalSpacing = .9, NormalPushSpeed = 1.5, BossPushSpeed = 2;
        private readonly MovementSettings settings;
        private readonly Dictionary<GridCell, List<BodySnapshot>> cells = new Dictionary<GridCell, List<BodySnapshot>>();
        private readonly List<List<BodySnapshot>> cellPool = new List<List<BodySnapshot>>();
        private readonly List<long> bossCandidates = new List<long>();
        private double maximumNormalRadius;

        public CrowdSolver(MovementSettings settings = null) { this.settings = settings ?? new MovementSettings(); }

        internal void BeginStep(List<UnitModel> units) => BuildGrid(units);

        internal DVec2 ConstrainNormalMovement(UnitModel unit, DVec2 movement)
        {
            double initialLengthSquared = DVec2.Dot(movement, movement);
            if (settings.CrowdAvoidanceStrength == 0 || initialLengthSquared < 1e-16) return movement;
            var origin = GridCell.At(unit.Position);
            double localX = unit.Position.Local.X - origin.X, localY = unit.Position.Local.Y - origin.Y;
            double radius = (unit.BodyRadius + maximumNormalRadius) * NormalSpacing + Math.Sqrt(initialLengthSquared);
            int minX = (int)Math.Floor(localX - radius), maxX = (int)Math.Floor(localX + radius);
            int minY = (int)Math.Floor(localY - radius), maxY = (int)Math.Floor(localY + radius);
            double resultX = movement.X, resultY = movement.Y;
            for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++)
            {
                if (!cells.TryGetValue(OffsetCell(origin, x, y), out var cell)) continue;
                int samples = Math.Min(SamplesPerCell, cell.Count);
                int offset = cell.Count <= SamplesPerCell ? 0 : (int)((ulong)unit.Id % (ulong)cell.Count);
                for (int sample = 0; sample < samples; sample++)
                {
                    var other = cell[cell.Count <= SamplesPerCell ? sample : (offset + sample * cell.Count / samples) % cell.Count];
                    if (other.Id == unit.Id) continue;
                    double awayX = localX - (other.X + x), awayY = localY - (other.Y + y);
                    double closing = awayX * resultX + awayY * resultY;
                    if (closing >= 0) continue;
                    double spacing = (unit.BodyRadius + other.Radius) * NormalSpacing;
                    double squared = awayX * awayX + awayY * awayY;
                    double clearance = squared - spacing * spacing;
                    // Contact limits inward travel, not the AI's intent. Outward movement and attack knockback stay free.
                    if (clearance <= 0)
                    {
                        if (squared < 1e-16) continue;
                        double blocked = closing / squared;
                        resultX -= awayX * blocked; resultY -= awayY * blocked;
                        continue;
                    }
                    double lengthSquared = resultX * resultX + resultY * resultY;
                    if (lengthSquared < 1e-16) return DVec2.Zero;
                    double discriminant = closing * closing - lengthSquared * clearance;
                    if (discriminant < 0) continue;
                    double hit = (-closing - Math.Sqrt(discriminant)) / lengthSquared;
                    if (hit >= 1) continue;
                    double normalX = awayX + resultX * hit, normalY = awayY + resultY * hit;
                    double inward = Math.Min(0, (normalX * resultX + normalY * resultY) * (1 - hit)) / (spacing * spacing);
                    // Keep tangential travel, otherwise two neighbours aiming at the player can stop an entire column.
                    resultX -= normalX * inward; resultY -= normalY * inward;
                }
            }
            return new DVec2(resultX, resultY);
        }

        internal void Resolve(WorldStore world, List<UnitModel> units, HashSet<long> knockedBack, double dt)
        {
            BuildGrid(units);
            if (maximumNormalRadius == 0) return;
            if (settings.CrowdAvoidanceStrength > 0)
                foreach (var unit in units)
                    if (unit.IsAlive && unit.Kind == UnitKind.Normal && !knockedBack.Contains(unit.Id))
                        MoveNormal(world, unit, NormalPush(unit, dt));
            // Bosses have right of way. Only the normal's position changes, even when it is pinned by terrain.
            foreach (var boss in units)
                if (boss.IsAlive && boss.Kind == UnitKind.Boss) PushFromBoss(world, boss, knockedBack, dt);
        }

        private void BuildGrid(List<UnitModel> units)
        {
            cells.Clear(); maximumNormalRadius = 0; int usedCells = 0;
            foreach (var unit in units)
            {
                if (!unit.IsAlive || unit.Kind != UnitKind.Normal) continue;
                maximumNormalRadius = Math.Max(maximumNormalRadius, unit.BodyRadius);
                var key = GridCell.At(unit.Position);
                if (!cells.TryGetValue(key, out var cell))
                {
                    if (usedCells == cellPool.Count) cellPool.Add(new List<BodySnapshot>(8));
                    cell = cellPool[usedCells++]; cell.Clear(); cells.Add(key, cell);
                }
                cell.Add(new BodySnapshot(unit, key));
            }
        }

        private DVec2 NormalPush(UnitModel unit, double dt)
        {
            var origin = GridCell.At(unit.Position);
            double searchRadius = (unit.BodyRadius + maximumNormalRadius) * NormalSpacing;
            double localX = unit.Position.Local.X - origin.X, localY = unit.Position.Local.Y - origin.Y;
            int minX = (int)Math.Floor(localX - searchRadius), maxX = (int)Math.Floor(localX + searchRadius);
            int minY = (int)Math.Floor(localY - searchRadius), maxY = (int)Math.Floor(localY + searchRadius);
            double correctionX = 0, correctionY = 0; int overlaps = 0;
            for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++)
            {
                if (!cells.TryGetValue(OffsetCell(origin, x, y), out var cell)) continue;
                int samples = Math.Min(SamplesPerCell, cell.Count);
                int offset = cell.Count <= SamplesPerCell ? 0 : (int)((ulong)unit.Id % (ulong)cell.Count);
                for (int sample = 0; sample < samples; sample++)
                {
                    // Evenly spaced representatives keep a fully stacked cell from becoming an all-pairs scan.
                    var other = cell[cell.Count <= SamplesPerCell ? sample : (offset + sample * cell.Count / samples) % cell.Count];
                    if (other.Id == unit.Id) continue;
                    // Neighbour-cell offsets are exact even at huge world coordinates. No repeated world conversion.
                    double awayX = localX - (other.X + x), awayY = localY - (other.Y + y);
                    double spacing = (unit.BodyRadius + other.Radius) * NormalSpacing;
                    double squared = awayX * awayX + awayY * awayY;
                    if (squared >= spacing * spacing) continue;
                    double distance = Math.Sqrt(squared);
                    if (distance > 1e-9)
                    {
                        double scale = (spacing - distance) * .5 / distance;
                        correctionX += awayX * scale; correctionY += awayY * scale;
                    }
                    else
                    {
                        var normal = StableNormal(unit.Id, other.Id);
                        correctionX += normal.X * spacing * .5; correctionY += normal.Y * spacing * .5;
                    }
                    overlaps++;
                }
            }
            if (overlaps == 0) return DVec2.Zero;
            // Sum contact pressure: averaging makes a dense crowd's resistance weaker as more bodies arrive.
            var correction = new DVec2(correctionX, correctionY) * settings.CrowdAvoidanceStrength;
            return Limit(correction, unit.MoveSpeed * NormalPushSpeed * dt);
        }

        private static GridCell OffsetCell(GridCell origin, int x, int y)
        {
            // The common body sizes touch only adjacent cells. Keep the general path for larger definitions.
            if (Math.Abs(x) >= ChunkData.Size || Math.Abs(y) >= ChunkData.Size) return origin.Offset(x, y);
            long chunkX = origin.Chunk.X, chunkY = origin.Chunk.Y;
            int localX = origin.X + x, localY = origin.Y + y;
            if (localX < 0) { chunkX = checked(chunkX - 1); localX += ChunkData.Size; }
            else if (localX >= ChunkData.Size) { chunkX = checked(chunkX + 1); localX -= ChunkData.Size; }
            if (localY < 0) { chunkY = checked(chunkY - 1); localY += ChunkData.Size; }
            else if (localY >= ChunkData.Size) { chunkY = checked(chunkY + 1); localY -= ChunkData.Size; }
            return new GridCell(new ChunkCoord(chunkX, chunkY), localX, localY);
        }

        private void PushFromBoss(WorldStore world, UnitModel boss, HashSet<long> knockedBack, double dt)
        {
            var travel = boss.PreviousPosition.DisplacementTo(boss.Position);
            double length = travel.Length;
            var direction = length > 1e-9 ? travel / length : DVec2.Zero;
            var side = new DVec2(-direction.Y, direction.X);
            double maximumPush = Math.Max(boss.MoveSpeed * BossPushSpeed * dt, length);
            // Cover the whole travelled capsule, including normals passed during a fast charge tick.
            world.Query.QueryCircle(boss.PreviousPosition.Offset(travel * .5),
                length * .5 + boss.BodyRadius + maximumNormalRadius, bossCandidates);
            foreach (long id in bossCandidates)
            {
                var normal = world.Units.Get(id);
                if (!normal.IsAlive || normal.Kind != UnitKind.Normal || knockedBack.Contains(id)) continue;
                var relative = boss.PreviousPosition.DisplacementTo(normal.Position);
                double along = DVec2.Dot(relative, direction), closest = Math.Max(0, Math.Min(length, along));
                var away = relative - direction * closest;
                double spacing = boss.BodyRadius + normal.BodyRadius, squared = DVec2.Dot(away, away);
                if (squared >= spacing * spacing) continue;
                DVec2 push;
                if (length > 1e-9)
                {
                    double across = DVec2.Dot(relative, side), endGap = along - closest;
                    double clearance = Math.Sqrt(Math.Max(0, spacing * spacing - endGap * endGap));
                    double sign = Math.Abs(across) > 1e-9 ? Math.Sign(across) :
                        (DVec2.Dot(StableNormal(id, boss.Id), side) >= 0 ? 1 : -1);
                    push = side * (sign * Math.Min(maximumPush, clearance - Math.Abs(across)));
                }
                else
                {
                    double distance = Math.Sqrt(squared);
                    push = (distance > 1e-9 ? away / distance : StableNormal(id, boss.Id)) * Math.Min(maximumPush, spacing - distance);
                }
                MoveNormal(world, normal, push);
            }
        }

        private void MoveNormal(WorldStore world, UnitModel unit, DVec2 push)
        {
            if (DVec2.Dot(push, push) < 1e-16) return;
            var destination = CircleSweep.MoveAndSlide(world.Query, unit.Position, push, unit.BodyRadius, settings.SlideContacts);
            if (!destination.Equals(unit.Position)) world.MoveUnit(unit.Id, destination);
        }

        private static DVec2 Limit(DVec2 vector, double maximum)
        {
            double length = vector.Length;
            return length > maximum ? vector * (maximum / length) : vector;
        }

        private static DVec2 StableNormal(long a, long b)
        {
            ulong bits = StableHash.Combine(71, Math.Min(a, b), Math.Max(a, b), 1);
            double angle = (bits & 65535) * (Math.PI * 2 / 65536);
            var normal = new DVec2(Math.Cos(angle), Math.Sin(angle)); return a < b ? normal : -normal;
        }

        private readonly struct BodySnapshot
        {
            public readonly long Id;
            public readonly double X, Y, Radius;
            public BodySnapshot(UnitModel unit, GridCell cell)
            { Id = unit.Id; X = unit.Position.Local.X - cell.X; Y = unit.Position.Local.Y - cell.Y; Radius = unit.BodyRadius; }
        }
    }
}
