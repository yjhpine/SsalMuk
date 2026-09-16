using System.Collections.Generic;
using System;

namespace SsalMuk.Core
{
    public sealed class AttackScheduler
    {
        private bool armed;
        private double next;
        public void Reset() => armed = false;
        public IEnumerable<double> CollectDueTimes(double from, double to, double period, bool hasTarget)
        {
            if (from < 0 || to < from || double.IsNaN(from) || double.IsNaN(to) || double.IsInfinity(to) ||
                period <= 0 || double.IsNaN(period) || double.IsInfinity(period)) throw new ArgumentOutOfRangeException(nameof(period));
            if (!hasTarget) { Reset(); yield break; }
            if (!armed) { armed = true; next = from; }
            while (armed && next <= to)
            {
                double due = next; double following = due + period;
                if (following <= due || double.IsInfinity(following)) throw new OverflowException("Attack period is not representable on this clock.");
                next = following; yield return due;
            }
        }
    }
}
