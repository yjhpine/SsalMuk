using System;
namespace SsalMuk.Core
{
    public readonly struct TargetCircle
    {
        public long Id { get; }
        public DVec2 Center { get; }
        public double Radius { get; }
        public TargetCircle(long id, DVec2 center, double radius)
        {
            if (id <= 0 || radius < 0 || double.IsNaN(radius) || double.IsInfinity(radius)) throw new ArgumentOutOfRangeException(nameof(radius));
            Id = id; Center = center; Radius = radius;
        }
    }
}
