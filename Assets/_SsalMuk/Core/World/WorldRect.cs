using System;
namespace SsalMuk.Core
{
    public readonly struct WorldRect
    {
        public WorldPosition Center { get; }
        public double HalfWidth { get; }
        public double HalfHeight { get; }
        public WorldRect(WorldPosition center, double halfWidth, double halfHeight)
        {
            if (!(halfWidth > 0) || !(halfHeight > 0) || double.IsInfinity(halfWidth) || double.IsInfinity(halfHeight)) throw new ArgumentOutOfRangeException(nameof(halfWidth));
            Center = center; HalfWidth = halfWidth; HalfHeight = halfHeight;
        }
        public bool Contains(WorldPosition position, double padding = 0)
        {
            if (padding < 0 || double.IsNaN(padding) || double.IsInfinity(padding)) throw new ArgumentOutOfRangeException(nameof(padding));
            var delta = Center.DisplacementTo(position);
            return Math.Abs(delta.X) <= HalfWidth + padding && Math.Abs(delta.Y) <= HalfHeight + padding;
        }
    }
}
