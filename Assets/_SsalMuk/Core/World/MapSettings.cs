using System;

namespace SsalMuk.Core
{
    public sealed class MapSettings
    {
        public double ObstacleChance { get; }
        public int GeneratorVersion { get; }
        public int PassageWidth { get; }
        public double MaximumBodyRadius { get; }
        public MapSettings(double obstacleChance = 0.2, int generatorVersion = 2, int passageWidth = 4, double maximumBodyRadius = 0.75)
        {
            if (double.IsNaN(obstacleChance) || obstacleChance < 0 || obstacleChance > 1) throw new ArgumentOutOfRangeException(nameof(obstacleChance));
            if (generatorVersion <= 0) throw new ArgumentOutOfRangeException(nameof(generatorVersion));
            if (passageWidth < 4 || passageWidth > 8) throw new ArgumentOutOfRangeException(nameof(passageWidth));
            if (maximumBodyRadius <= 0 || double.IsNaN(maximumBodyRadius) || double.IsInfinity(maximumBodyRadius) || maximumBodyRadius * 2 + 1 > passageWidth)
                throw new ArgumentOutOfRangeException(nameof(maximumBodyRadius), "Passages need body clearance at cell centers.");
            ObstacleChance = obstacleChance; GeneratorVersion = generatorVersion;
            PassageWidth = passageWidth; MaximumBodyRadius = maximumBodyRadius;
        }
        public static MapSettings TestDefaults(double obstacleChance = 0.2) => new MapSettings(obstacleChance);
    }
}
