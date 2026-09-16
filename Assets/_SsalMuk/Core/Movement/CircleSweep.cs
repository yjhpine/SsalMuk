using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    public static class CircleSweep
    {
        private const double Epsilon = 1e-10;

        public static WorldPosition MoveAndSlide(IWorldQuery query, WorldPosition start, DVec2 displacement, double radius, int maximumContacts = 4)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (maximumContacts <= 0) throw new ArgumentOutOfRangeException(nameof(maximumContacts));
            if (!query.IsCircleFree(start, radius)) throw new InvalidOperationException("Movement requires a safe starting position.");
            var position = start; var remaining = displacement;
            for (int i = 0; i < maximumContacts && remaining.Length > 1e-9; i++)
            {
                var hit = query.SweepCircle(position, remaining, radius);
                if (!hit.HasValue) { position = position.Offset(remaining); break; }
                double fraction = Math.Max(0, hit.Value.Fraction - 1e-7 / Math.Max(remaining.Length, 1e-7));
                position = position.Offset(remaining * fraction);
                remaining *= 1 - fraction;
                double inward = DVec2.Dot(remaining, hit.Value.Normal);
                if (inward < 0) remaining -= hit.Value.Normal * inward;
                else break;
            }
            if (!query.IsCircleFree(position, radius)) throw new InvalidOperationException("Obstacle penetration after movement correction.");
            return position;
        }

        public static bool OverlapsBox(DVec2 center, double radius, DVec2 min, DVec2 max)
        {
            if (center.X > min.X && center.X < max.X && center.Y > min.Y && center.Y < max.Y) return true;
            double dx = Math.Max(0, Math.Max(min.X - center.X, center.X - max.X));
            double dy = Math.Max(0, Math.Max(min.Y - center.Y, center.Y - max.Y));
            return dx * dx + dy * dy < radius * radius - Epsilon;
        }

        public static SweepHit? AgainstBox(DVec2 start, DVec2 displacement, double radius, DVec2 min, DVec2 max)
        {
            SpatialIndex.RequireRadius(radius);
            if (min.X > max.X || min.Y > max.Y) throw new ArgumentException("Box bounds are reversed.");
            if (OverlapsBox(start, radius, min, max)) throw new InvalidOperationException("Sweep starts inside an obstacle.");
            double best = double.PositiveInfinity; DVec2 normal = default;
            if (displacement.X > Epsilon) Face((min.X - radius - start.X) / displacement.X, new DVec2(-1, 0), true);
            if (displacement.X < -Epsilon) Face((max.X + radius - start.X) / displacement.X, new DVec2(1, 0), true);
            if (displacement.Y > Epsilon) Face((min.Y - radius - start.Y) / displacement.Y, new DVec2(0, -1), false);
            if (displacement.Y < -Epsilon) Face((max.Y + radius - start.Y) / displacement.Y, new DVec2(0, 1), false);
            Corner(new DVec2(min.X, min.Y), -1, -1); Corner(new DVec2(min.X, max.Y), -1, 1);
            Corner(new DVec2(max.X, min.Y), 1, -1); Corner(new DVec2(max.X, max.Y), 1, 1);
            return double.IsPositiveInfinity(best) ? (SweepHit?)null : new SweepHit(best, normal, WorldPosition.FromLocal(start + displacement * best));

            void Face(double t, DVec2 n, bool vertical)
            {
                if (t < -Epsilon || t > 1 || t >= best) return;
                var p = start + displacement * Math.Max(0, t);
                double along = vertical ? p.Y : p.X, low = vertical ? min.Y : min.X, high = vertical ? max.Y : max.X;
                if (along < low - Epsilon || along > high + Epsilon) return;
                best = Math.Max(0, t); normal = n;
            }
            void Corner(DVec2 corner, int sx, int sy)
            {
                var offset = start - corner;
                double a = DVec2.Dot(displacement, displacement);
                if (a <= Epsilon) return;
                double b = DVec2.Dot(offset, displacement), c = DVec2.Dot(offset, offset) - radius * radius;
                double discriminant = b * b - a * c;
                if (discriminant < 0) return;
                double t = (-b - Math.Sqrt(discriminant)) / a;
                if (t < -Epsilon || t > 1 || t >= best) return;
                var delta = start + displacement * Math.Max(0, t) - corner;
                if (delta.X * sx < -Epsilon || delta.Y * sy < -Epsilon || DVec2.Dot(displacement, delta) >= -Epsilon) return;
                best = Math.Max(0, t); normal = delta.Normalized;
            }
        }

        // Traverse the segment's grid cells rather than its potentially huge rectangular bounding area.
        internal static IEnumerable<GridCell> CandidateCells(WorldPosition start, DVec2 displacement, double radius)
        {
            SpatialIndex.RequireRadius(radius);
            if (radius >= int.MaxValue - 1) throw new OverflowException("Sweep radius exceeds grid enumeration precision.");
            int range = (int)Math.Ceiling(radius);
            var visited = new HashSet<GridCell>(); var cell = GridCell.At(start);
            int stepX = Math.Sign(displacement.X), stepY = Math.Sign(displacement.Y);
            double fractionX = start.Local.X - Math.Floor(start.Local.X), fractionY = start.Local.Y - Math.Floor(start.Local.Y);
            double nextX = stepX == 0 ? double.PositiveInfinity : (stepX > 0 ? 1 - fractionX : fractionX) / Math.Abs(displacement.X);
            double nextY = stepY == 0 ? double.PositiveInfinity : (stepY > 0 ? 1 - fractionY : fractionY) / Math.Abs(displacement.Y);
            double incrementX = stepX == 0 ? double.PositiveInfinity : 1 / Math.Abs(displacement.X);
            double incrementY = stepY == 0 ? double.PositiveInfinity : 1 / Math.Abs(displacement.Y);
            while (true)
            {
                for (int y = -range; y <= range; y++) for (int x = -range; x <= range; x++)
                {
                    var candidate = cell.Offset(x, y);
                    if (visited.Add(candidate)) yield return candidate;
                }
                if (nextX > 1 && nextY > 1) yield break;
                if (nextX < nextY) { cell = cell.Offset(stepX, 0); nextX += incrementX; }
                else if (nextY < nextX) { cell = cell.Offset(0, stepY); nextY += incrementY; }
                else { cell = cell.Offset(stepX, stepY); nextX += incrementX; nextY += incrementY; }
            }
        }
    }
}
