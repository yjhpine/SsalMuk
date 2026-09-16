using System;

namespace SsalMuk.Core
{
    public sealed class AiSettings
    {
        public double SearchDistance { get; }
        public double EmergencyContactSeconds { get; }
        public int DirectionCount { get; }
        public double ProbeDistance { get; }
        public double EnterBlockedFraction { get; }
        public double ReleaseBlockedFraction { get; }
        public double MinimumHoldSeconds { get; }
        public double ReleaseStableSeconds { get; }
        public double NoProgressSeconds { get; }
        public double RiskWeight { get; }
        public double DistanceWeight { get; }

        public AiSettings(double searchDistance = 12, double emergencyContactSeconds = 0.35, int directionCount = 16,
            double probeDistance = 2.4, double enterBlockedFraction = 0.7, double releaseBlockedFraction = 0.35,
            double minimumHoldSeconds = 0.45, double releaseStableSeconds = 0.45, double noProgressSeconds = 0.8,
            double riskWeight = 2, double distanceWeight = 1)
        {
            foreach (double value in new[] { searchDistance, emergencyContactSeconds, probeDistance, minimumHoldSeconds,
                releaseStableSeconds, noProgressSeconds, riskWeight, distanceWeight })
                if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (directionCount < 4 || directionCount > 128) throw new ArgumentOutOfRangeException(nameof(directionCount));
            if (!(releaseBlockedFraction >= 0 && releaseBlockedFraction < enterBlockedFraction && enterBlockedFraction <= 1))
                throw new ArgumentOutOfRangeException(nameof(enterBlockedFraction));
            SearchDistance = searchDistance; EmergencyContactSeconds = emergencyContactSeconds; DirectionCount = directionCount;
            ProbeDistance = probeDistance; EnterBlockedFraction = enterBlockedFraction; ReleaseBlockedFraction = releaseBlockedFraction;
            MinimumHoldSeconds = minimumHoldSeconds; ReleaseStableSeconds = releaseStableSeconds; NoProgressSeconds = noProgressSeconds;
            RiskWeight = riskWeight; DistanceWeight = distanceWeight;
        }
    }
}
