using System;
using System.Numerics;

namespace SsalMuk.Core
{
    public sealed class ProgressionService
    {
        private readonly RunModel run;
        public ProgressionService(RunModel run) => this.run = run ?? throw new ArgumentNullException(nameof(run));
        public bool AddExperience(BigInteger amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (run.Phase != RunPhase.Running || run.Player == null || !run.Player.IsAlive) return false;
            var growth = run.Player.Growth; var settings = run.GrowthSettings;
            BigInteger available = growth.ExperienceIntoLevel + amount, lo = 0, hi = 1;
            while (settings.CostForLevels(growth.Level, hi) <= available) { lo = hi; hi *= 2; }
            while (hi - lo > 1)
            {
                BigInteger mid = (hi + lo) / 2;
                if (settings.CostForLevels(growth.Level, mid) <= available) lo = mid; else hi = mid;
            }
            growth.ExperienceIntoLevel = available - settings.CostForLevels(growth.Level, lo);
            growth.Level += lo; growth.PendingChoices += lo; return true;
        }
    }
}
