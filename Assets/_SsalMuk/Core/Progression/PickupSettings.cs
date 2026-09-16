using System;
using System.Numerics;
namespace SsalMuk.Core
{
    public sealed class PickupSettings
    {
        public double AttractionRadius { get; }
        public double FlightSpeed { get; }
        public double OrbRadius { get; }
        public BigInteger BlueThreshold { get; }
        public BigInteger RedThreshold { get; }
        public PickupSettings(double attractionRadius = 1.5, double flightSpeed = 6, double orbRadius = 0.08,
            BigInteger? blueThreshold = null, BigInteger? redThreshold = null)
        {
            foreach (double value in new[] { attractionRadius, flightSpeed, orbRadius })
                if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
            BlueThreshold = blueThreshold ?? 5; RedThreshold = redThreshold ?? 25;
            if (BlueThreshold <= 0 || RedThreshold <= BlueThreshold) throw new ArgumentOutOfRangeException(nameof(redThreshold));
            AttractionRadius = attractionRadius; FlightSpeed = flightSpeed; OrbRadius = orbRadius;
        }
        public static PickupSettings TestDefaults() => new PickupSettings();
        public ExperienceTier TierFor(BigInteger value)
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            return value >= RedThreshold ? ExperienceTier.Red : value >= BlueThreshold ? ExperienceTier.Blue : ExperienceTier.Green;
        }
        public void ValidateForPlayer(double bodyRadius)
        {
            if (bodyRadius <= 0 || double.IsNaN(bodyRadius) || double.IsInfinity(bodyRadius)) throw new ArgumentOutOfRangeException(nameof(bodyRadius));
            if (AttractionRadius <= bodyRadius + OrbRadius) throw new ArgumentException("Attraction radius must exceed the body contact distance.");
        }
    }
}
