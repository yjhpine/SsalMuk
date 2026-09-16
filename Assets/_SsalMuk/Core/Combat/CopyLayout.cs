using System;
using System.Numerics;
namespace SsalMuk.Core
{
    public static class CopyLayout
    {
        public static DVec2 Offset(WeaponKind kind, BigInteger index, double spacing)
        {
            if (index < 0 || !Enum.IsDefined(typeof(WeaponKind), kind)) throw new ArgumentOutOfRangeException(nameof(index));
            WeaponDefinition.RequirePositive(spacing, nameof(spacing));
            if (index.IsZero || kind == WeaponKind.Axe) return DVec2.Zero;
            BigInteger row = (index + 1) / 2;
            double offset = (double)row * spacing, previous = (double)(row - 1) * spacing;
            if (double.IsInfinity(offset) || !(offset > previous)) throw new NumericRangeException("Weapon copy offset lost numeric range or spacing.");
            return new DVec2(0, index.IsEven ? -offset : offset);
        }
        public static double Phase(BigInteger index, int degrees = 20)
        {
            if (index < 0 || degrees <= 0) throw new ArgumentOutOfRangeException(nameof(index));
            double angle = (double)((((index + 1) / 2) % 360) * (degrees % 360) % 360) * Math.PI / 180;
            return index.IsEven ? -angle : angle;
        }
    }
}
