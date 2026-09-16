using System;

namespace SsalMuk.Core
{
    public readonly struct DVec2 : IEquatable<DVec2>
    {
        public double X { get; }
        public double Y { get; }
        public static DVec2 Zero => default;

        public DVec2(double x, double y)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) throw new ArgumentOutOfRangeException(nameof(x));
            if (double.IsNaN(y) || double.IsInfinity(y)) throw new ArgumentOutOfRangeException(nameof(y));
            X = x; Y = y;
        }

        public double Length
        {
            get
            {
                double scale = Math.Max(Math.Abs(X), Math.Abs(Y));
                if (scale == 0) return 0;
                double x = X / scale, y = Y / scale;
                return scale * Math.Sqrt(x * x + y * y);
            }
        }

        public DVec2 Normalized
        {
            get
            {
                double scale = Math.Max(Math.Abs(X), Math.Abs(Y));
                if (scale == 0) return Zero;
                var scaled = new DVec2(X / scale, Y / scale);
                return scaled / scaled.Length;
            }
        }

        public static double Dot(DVec2 a, DVec2 b) => a.X * b.X + a.Y * b.Y;
        public static DVec2 operator +(DVec2 a, DVec2 b) => new DVec2(a.X + b.X, a.Y + b.Y);
        public static DVec2 operator -(DVec2 a, DVec2 b) => new DVec2(a.X - b.X, a.Y - b.Y);
        public static DVec2 operator -(DVec2 value) => new DVec2(-value.X, -value.Y);
        public static DVec2 operator *(DVec2 value, double scale) => new DVec2(value.X * scale, value.Y * scale);
        public static DVec2 operator *(double scale, DVec2 value) => value * scale;
        public static DVec2 operator /(DVec2 value, double divisor) => new DVec2(value.X / divisor, value.Y / divisor);
        public bool Equals(DVec2 other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is DVec2 other && Equals(other);
        public override int GetHashCode() => unchecked(X.GetHashCode() * 397 ^ Y.GetHashCode());
        public static bool operator ==(DVec2 a, DVec2 b) => a.Equals(b);
        public static bool operator !=(DVec2 a, DVec2 b) => !a.Equals(b);
    }
}
