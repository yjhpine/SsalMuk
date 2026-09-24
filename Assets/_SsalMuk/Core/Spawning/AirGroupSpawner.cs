using System;
namespace SsalMuk.Core
{
    public sealed class AirGroupSpawner
    {
        public DVec2 Direction { get; }
        private readonly WorldPosition anchor;
        private readonly long count, centerCount, sideCount;
        private readonly double spacing;
        public AirGroupSpawner(WorldRect view, WorldPosition player, long count, double bodyRadius, SpawnSettings settings, IRandomSource random)
        {
            if (count <= 0 || settings == null || random == null) throw new ArgumentException("An air group needs a count, settings and random source.");
            WeaponDefinition.RequirePositive(bodyRadius, nameof(bodyRadius));
            this.count = count; spacing = settings.AirSpacing;
            sideCount = count >= 3 ? Math.Max(1, (count - 2) / 3) : 0;
            centerCount = count - 2 * sideCount;
            double length = (centerCount - 1) * spacing * 2;
            if (double.IsInfinity(length) || (count > 1 && (double)count == (double)(count - 1))) throw new NumericRangeException("Air group spacing is not representable.");
            int side = (int)(Draw(random) * 4); double tangent = Draw(random) * 1.6 - 0.8;
            // Bound the whole transverse formation, including oblique approaches at the screen corners.
            double margin = settings.GroundMargin + bodyRadius + length * .5 + (sideCount > 0 ? spacing : 0);
            var offset = side < 2 ? new DVec2((side == 0 ? 1 : -1) * (view.HalfWidth + margin), tangent * view.HalfHeight) :
                new DVec2(tangent * view.HalfWidth, (side == 2 ? 1 : -1) * (view.HalfHeight + margin));
            anchor = view.Center.Offset(offset); var toward = anchor.DisplacementTo(player);
            Direction = toward == DVec2.Zero ? -offset.Normalized : toward.Normalized;
        }
        public WorldPosition Position(long index)
        {
            if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
            // Three ranks face the player: the long axis crosses the flight direction.
            long row = index; int lane = 0; double inset = 0;
            if (index >= centerCount)
            {
                long sideIndex = index - centerCount;
                lane = sideIndex < sideCount ? -1 : 1;
                row = sideIndex % sideCount;
                inset = (centerCount - sideCount) * .5;
            }
            var across = new DVec2(-Direction.Y, Direction.X);
            double transverse = (row + inset - (centerCount - 1) * .5) * spacing * 2;
            return anchor.Offset(across * transverse - Direction * (lane * spacing));
        }
        internal static double Draw(IRandomSource random)
        {
            double value = random.NextUnit(); if (!(value >= 0 && value < 1)) throw new ArgumentException("Random values must be in [0, 1)."); return value;
        }
    }
}
