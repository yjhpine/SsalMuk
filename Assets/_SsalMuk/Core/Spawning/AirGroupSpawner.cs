using System;
namespace SsalMuk.Core
{
    public sealed class AirGroupSpawner
    {
        public DVec2 Direction { get; }
        private readonly WorldPosition anchor;
        private readonly long count;
        private readonly double spacing;
        public AirGroupSpawner(WorldRect view, WorldPosition player, long count, double bodyRadius, SpawnSettings settings, IRandomSource random)
        {
            if (count <= 0 || settings == null || random == null) throw new ArgumentException("An air group needs a count, settings and random source.");
            WeaponDefinition.RequirePositive(bodyRadius, nameof(bodyRadius));
            this.count = count; spacing = settings.AirSpacing;
            double spread = (count - 1) * spacing / 2;
            if (double.IsInfinity(spread) || (count > 1 && (double)count == (double)(count - 1))) throw new NumericRangeException("Air group spacing is not representable.");
            int side = (int)(Draw(random) * 4); double tangent = Draw(random) * 1.6 - 0.8;
            double margin = settings.GroundMargin + bodyRadius + spread;
            var offset = side < 2 ? new DVec2((side == 0 ? 1 : -1) * (view.HalfWidth + margin), tangent * view.HalfHeight) :
                new DVec2(tangent * view.HalfWidth, (side == 2 ? 1 : -1) * (view.HalfHeight + margin));
            anchor = view.Center.Offset(offset); var toward = anchor.DisplacementTo(player);
            Direction = toward == DVec2.Zero ? -offset.Normalized : toward.Normalized;
        }
        public WorldPosition Position(long index)
        {
            if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
            return anchor.Offset(new DVec2(-Direction.Y, Direction.X) * ((index - (count - 1) / 2.0) * spacing));
        }
        internal static double Draw(IRandomSource random)
        {
            double value = random.NextUnit(); if (!(value >= 0 && value < 1)) throw new ArgumentException("Random values must be in [0, 1)."); return value;
        }
    }
}
