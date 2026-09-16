using System;
using System.Collections.Generic;
namespace SsalMuk.Core
{
    public sealed class BossSchedule
    {
        private readonly double interval;
        private long ordinal = 1;
        private double lastObserved;
        public BossSchedule(double interval) { WeaponDefinition.RequirePositive(interval, nameof(interval)); this.interval = interval; }
        public IReadOnlyList<double> CollectDueTimes(double now)
        {
            if (now < lastObserved || double.IsNaN(now) || double.IsInfinity(now)) throw new ArgumentOutOfRangeException(nameof(now));
            List<double> due = null;
            while (ordinal * interval <= now)
            {
                double at = ordinal * interval; long next = checked(ordinal + 1);
                if (next * interval <= at || double.IsInfinity(next * interval)) throw new NumericRangeException("A periodic spawn deadline lost time precision.");
                if (due == null) due = new List<double>(); due.Add(at); ordinal = next;
            }
            lastObserved = now; return due == null ? (IReadOnlyList<double>)Array.Empty<double>() : due.AsReadOnly();
        }
    }
}
