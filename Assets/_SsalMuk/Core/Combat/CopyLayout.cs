using System;
using System.Numerics;
namespace SsalMuk.Core
{
    public static class CopyLayout
    {
        public static int Degrees(WeaponKind kind)
        {
            switch (kind)
            {
                case WeaponKind.Sword: case WeaponKind.Spear: case WeaponKind.Axe: return 40;
                case WeaponKind.Fireball: return 30;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
        // One extra copy per level: center, left one interval, right one interval, left two, right two.
        public static double Phase(BigInteger index, int degrees = 40)
        {
            if (index < 0 || degrees <= 0) throw new ArgumentOutOfRangeException(nameof(index));
            double angle = (double)((((index + 1) / 2) % 360) * (degrees % 360) % 360) * Math.PI / 180;
            return index.IsEven ? -angle : angle;
        }
    }
}
