using System;
namespace SsalMuk.Core
{
    public sealed class RewardWeights
    {
        public double Acquisition { get; }
        public double Upgrade { get; }
        public RewardWeights(double acquisition = 1, double upgrade = 3)
        {
            if (acquisition < 0 || double.IsNaN(acquisition) || double.IsInfinity(acquisition)) throw new ArgumentOutOfRangeException(nameof(acquisition));
            if (upgrade <= 0 || double.IsNaN(upgrade) || double.IsInfinity(upgrade))
                throw new ArgumentOutOfRangeException(nameof(upgrade), "Upgrades need positive weight so every ownership state has three choices.");
            Acquisition = acquisition; Upgrade = upgrade;
        }
        public static RewardWeights TestDefaults() => new RewardWeights();
    }
}
