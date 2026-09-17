using System;
namespace SsalMuk.Core
{
    public sealed class SpawnSettings
    {
        public const double BossInterval = 300;
        public double NormalBaseRate { get; }
        public double NormalRateGrowth { get; }
        public double AirInterval { get; }
        public int AirBaseCount { get; }
        public double AirCountGrowthSeconds { get; }
        public double AirSpacing { get; }
        public double GroundMargin { get; }
        public double GroundBand { get; }
        public int PositionAttempts { get; }
        public int CreationBudget { get; }
        public double AirDepartureMargin { get; }
        public int NormalPopulationCap { get; }
        public SpawnSettings(double normalBaseRate = 1, double normalRateGrowth = 1.0 / 120, double airInterval = 20,
            int airBaseCount = 8, double airCountGrowthSeconds = 120, double airSpacing = 0.65, double groundMargin = 1.2,
            double groundBand = 5, int positionAttempts = 32, int creationBudget = 64, double airDepartureMargin = 4, int normalPopulationCap = 300)
        {
            foreach (double value in new[] { normalBaseRate, airInterval, airCountGrowthSeconds, airSpacing, groundMargin, groundBand, airDepartureMargin })
                WeaponDefinition.RequirePositive(value, nameof(value));
            if (normalRateGrowth < 0 || double.IsNaN(normalRateGrowth) || double.IsInfinity(normalRateGrowth)) throw new ArgumentOutOfRangeException(nameof(normalRateGrowth));
            if (airBaseCount <= 0 || positionAttempts <= 0 || creationBudget <= 0) throw new ArgumentOutOfRangeException(nameof(creationBudget));
            if (normalPopulationCap <= 0) throw new ArgumentOutOfRangeException(nameof(normalPopulationCap));
            NormalBaseRate = normalBaseRate; NormalRateGrowth = normalRateGrowth; AirInterval = airInterval;
            AirBaseCount = airBaseCount; AirCountGrowthSeconds = airCountGrowthSeconds; AirSpacing = airSpacing;
            GroundMargin = groundMargin; GroundBand = groundBand; PositionAttempts = positionAttempts; CreationBudget = creationBudget;
            AirDepartureMargin = airDepartureMargin;
            NormalPopulationCap = normalPopulationCap;
        }
    }
}
