using System;

namespace SsalMuk.Core
{
    public static class AttackGeometry
    {
        public static bool SwordContains(DVec2 relative, double targetRadius, double reach) => SectorContains(relative, targetRadius, reach, Math.PI / 6);
        public static bool SwordSweepContains(DVec2 from, DVec2 to, double targetRadius, double reach, double previousProgress, double progress)
        {
            if (!(previousProgress >= 0 && progress >= previousProgress && progress <= 1)) throw new ArgumentOutOfRangeException(nameof(progress));
            double middle = (-0.5 + (previousProgress + progress) / 2) * Math.PI / 3;
            double half = (progress - previousProgress) * Math.PI / 6;
            from = Rotate(from, -middle); to = Rotate(to, -middle);
            if (SectorContains(from, targetRadius, reach, half) || SectorContains(to, targetRadius, reach, half)) return true;
            var lower = new DVec2(Math.Cos(half), -Math.Sin(half)) * reach;
            var upper = new DVec2(lower.X, -lower.Y);
            if (SegmentDistance(from, to, DVec2.Zero, lower) <= targetRadius + 1e-10 ||
                SegmentDistance(from, to, DVec2.Zero, upper) <= targetRadius + 1e-10) return true;
            if (CircleContact.Interval(from, to - from, reach + targetRadius, out double enter, out double exit))
            {
                var atEnter = from + (to - from) * enter; var atExit = from + (to - from) * exit;
                if (Math.Abs(Math.Atan2(atEnter.Y, atEnter.X)) <= half + 1e-10 || Math.Abs(Math.Atan2(atExit.Y, atExit.X)) <= half + 1e-10) return true;
            }
            return false;
        }
        private static bool SectorContains(DVec2 point, double radius, double reach, double half)
        {
            if (radius < 0 || double.IsNaN(radius) || double.IsInfinity(radius) || reach <= 0 || double.IsNaN(reach) || double.IsInfinity(reach))
                throw new ArgumentOutOfRangeException(nameof(reach));
            if (point.Length <= radius + 1e-10) return true;
            if (point.Length <= reach + radius + 1e-10 && Math.Abs(Math.Atan2(point.Y, point.X)) <= half + 1e-10) return true;
            var upper = new DVec2(Math.Cos(half), Math.Sin(half)) * reach;
            return PointSegmentDistance(point, DVec2.Zero, upper) <= radius + 1e-10 ||
                PointSegmentDistance(point, DVec2.Zero, new DVec2(upper.X, -upper.Y)) <= radius + 1e-10;
        }
        public static DVec2 Rotate(DVec2 vector, double radians)
        {
            double cosine = Math.Cos(radians), sine = Math.Sin(radians);
            return new DVec2(vector.X * cosine - vector.Y * sine, vector.X * sine + vector.Y * cosine);
        }
        public static double PointSegmentDistance(DVec2 point, DVec2 from, DVec2 to)
        {
            var segment = to - from; double lengthSquared = DVec2.Dot(segment, segment);
            double fraction = lengthSquared > 1e-20 ? Math.Max(0, Math.Min(1, DVec2.Dot(point - from, segment) / lengthSquared)) : 0;
            return (point - from - segment * fraction).Length;
        }
        private static double Cross(DVec2 a, DVec2 b) => a.X * b.Y - a.Y * b.X;
        private static double SegmentDistance(DVec2 a, DVec2 b, DVec2 c, DVec2 d)
        {
            var ab = b - a; var cd = d - c; double denominator = Cross(ab, cd);
            if (Math.Abs(denominator) > 1e-15)
            {
                double t = Cross(c - a, cd) / denominator, u = Cross(c - a, ab) / denominator;
                if (t >= 0 && t <= 1 && u >= 0 && u <= 1) return 0;
            }
            return Math.Min(Math.Min(PointSegmentDistance(a, c, d), PointSegmentDistance(b, c, d)),
                Math.Min(PointSegmentDistance(c, a, b), PointSegmentDistance(d, a, b)));
        }
    }
}
