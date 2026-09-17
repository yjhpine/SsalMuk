using System.Collections.Generic;
using System;
using System.Numerics;

namespace SsalMuk.Core
{
    public sealed class AttackScheduler
    {
        private bool burstArmed, inGroup;
        private double dueStrike, groupStart, groupEnd, groupPeriod, groupFraction;
        private BigInteger repeat, repeatCount;
        public BigInteger PendingStrikes => burstArmed ? (inGroup ? repeatCount - repeat : BigInteger.One) : BigInteger.Zero;
        public IEnumerable<ScheduledStrike> CollectDueStrikes(double from, double to, Func<WeaponStats> getStats, bool hasTarget)
        {
            if (from < 0 || to < from || double.IsNaN(from) || double.IsNaN(to) || double.IsInfinity(to)) throw new ArgumentOutOfRangeException(nameof(to));
            if (getStats == null) throw new ArgumentNullException(nameof(getStats));
            if (!hasTarget) { Reset(); yield break; }
            if (!burstArmed) { burstArmed = true; inGroup = false; dueStrike = from; }
            while (burstArmed && dueStrike <= to)
            {
                if (!inGroup)
                {
                    var stats = getStats() ?? throw new ArgumentException("A weapon needs calculated stats.");
                    groupStart = dueStrike; groupPeriod = stats.PeriodSeconds; groupFraction = stats.BurstFraction;
                    groupEnd = groupStart + groupPeriod; RequireLater(groupStart, groupEnd);
                    repeatCount = stats.Repeats; repeat = BigInteger.Zero;
                    if (repeatCount > 1) RequireLater(groupStart, groupStart + groupPeriod * groupFraction / (double)(repeatCount - 1));
                    inGroup = true;
                }
                double time = dueStrike; var index = repeat; repeat++;
                if (repeat < repeatCount)
                {
                    dueStrike = groupStart + groupPeriod * groupFraction * ((double)repeat / (double)(repeatCount - 1));
                    RequireLater(time, dueStrike);
                }
                else { dueStrike = groupEnd; inGroup = false; }
                yield return new ScheduledStrike(time, index);
            }
        }
        private static void RequireLater(double previous, double following)
        {
            if (!(following > previous) || double.IsInfinity(following)) throw new NumericRangeException("Attack schedule cannot represent every reserved deadline on this clock.");
        }
        private bool armed;
        private double next;
        public void Reset() { armed = burstArmed = inGroup = false; }
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
