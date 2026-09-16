using System;

namespace SsalMuk.Core
{
    public static class CircleContact
    {
        // Relative motion of two circles over the same interval, including initial overlap.
        public static bool Interval(DVec2 relativeStart, DVec2 relativeMovement, double combinedRadius, out double enter, out double exit)
        {
            enter = 0; exit = 1;
            double a = DVec2.Dot(relativeMovement, relativeMovement);
            double b = DVec2.Dot(relativeStart, relativeMovement);
            double c = DVec2.Dot(relativeStart, relativeStart) - combinedRadius * combinedRadius;
            if (a < 1e-20) return c <= 0;
            double discriminant = b * b - a * c;
            if (discriminant < 0) return false;
            double root = Math.Sqrt(discriminant);
            enter = Math.Max(0, (-b - root) / a); exit = Math.Min(1, (-b + root) / a);
            return enter <= exit && exit >= 0 && enter <= 1;
        }
    }
}
