using SsalMuk.Core;
using UnityEngine;
namespace SsalMuk.Unity
{
    public readonly struct WorldRenderOrigin
    {
        public WorldPosition Origin { get; }
        public WorldRenderOrigin(WorldPosition origin) { Origin = origin; }
        public Vector3 ToViewPosition(WorldPosition position) => ToVector(Origin.DisplacementTo(position));
        public static float ToFloat(double value)
        {
            float result = (float)value;
            if (float.IsInfinity(result) || float.IsNaN(result)) throw new NumericRangeException("A visible coordinate is outside Unity's float representation.");
            return result;
        }
        public static Vector3 ToVector(DVec2 relative) => new Vector3(ToFloat(relative.X), ToFloat(relative.Y), 0);
    }
}
