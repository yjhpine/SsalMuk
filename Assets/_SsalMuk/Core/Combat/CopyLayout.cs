using System;
using System.Numerics;
namespace SsalMuk.Core
{
    public static class CopyLayout
    {
        // 0, +15, -15, +30, -30, ... . Reduce integers before conversion, even for huge upgrade counts.
        public static double Phase(BigInteger index, int degrees = 15)
        {
            if (index < 0 || degrees <= 0) throw new ArgumentOutOfRangeException(nameof(index));
            double angle = (double)((((index + 1) / 2) % 360) * (degrees % 360) % 360) * Math.PI / 180;
            return index.IsEven ? -angle : angle;
        }
    }
}
