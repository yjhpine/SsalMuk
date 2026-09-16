using System;
using System.Collections.Generic;
namespace SsalMuk.Core
{
    public sealed class OfferGenerator
    {
        public IReadOnlyList<RewardId> Generate(IReadOnlyCollection<WeaponKind> owned, IRandomSource random, RewardWeights weights)
        {
            if (owned == null || owned.Count == 0) throw new ArgumentException("At least one owned weapon is required.", nameof(owned));
            if (random == null || weights == null) throw new ArgumentNullException(nameof(random));
            var set = new HashSet<WeaponKind>(owned);
            foreach (var kind in set) if (!Enum.IsDefined(typeof(WeaponKind), kind)) throw new ArgumentException("Unknown owned weapon.");
            var acquisitions = new List<RewardId>(); var upgrades = new List<RewardId>();
            foreach (WeaponKind kind in Enum.GetValues(typeof(WeaponKind)))
            {
                if (set.Contains(kind)) foreach (UpgradeKind upgrade in Enum.GetValues(typeof(UpgradeKind))) upgrades.Add(RewardId.Upgrade(kind, upgrade));
                else if (weights.Acquisition > 0) acquisitions.Add(RewardId.Acquire(kind));
            }
            if (acquisitions.Count + upgrades.Count < 3) throw new ArgumentException("Reward settings cannot produce three distinct choices.");
            var choices = new List<RewardId>(3);
            double maximum = Math.Max(weights.Acquisition, weights.Upgrade);
            double probability = (weights.Acquisition / maximum) / (weights.Acquisition / maximum + weights.Upgrade / maximum);
            for (int i = 0; i < 3; i++)
            {
                double category = Draw(random);
                var pool = acquisitions.Count > 0 && (upgrades.Count == 0 || category < probability) ? acquisitions : upgrades;
                int index = (int)(Draw(random) * pool.Count); choices.Add(pool[index]); pool.RemoveAt(index);
            }
            return choices.AsReadOnly();
        }
        private static double Draw(IRandomSource random)
        {
            double value = random.NextUnit();
            if (!(value >= 0 && value < 1)) throw new ArgumentException("Random values must be in [0, 1).");
            return value;
        }
    }
}
