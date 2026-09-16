using System;

namespace SsalMuk.Core
{
    public sealed class RunClock
    {
        public double FixedStep { get; }
        public long ElapsedTicks { get; private set; }
        public double ElapsedSeconds => ElapsedTicks * FixedStep;
        public bool IsRunning { get; private set; }

        public RunClock(double fixedStep)
        {
            if (fixedStep <= 0 || double.IsNaN(fixedStep) || double.IsInfinity(fixedStep))
                throw new ArgumentOutOfRangeException(nameof(fixedStep));
            FixedStep = fixedStep;
        }

        public void Start() => IsRunning = true;
        public void Stop() => IsRunning = false;
        public void Advance(int ticks = 1)
        {
            if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks));
            if (!IsRunning) return;
            long next = checked(ElapsedTicks + ticks);
            if (double.IsInfinity(next * FixedStep)) throw new OverflowException("Elapsed time is not representable.");
            ElapsedTicks = next;
        }
    }
}
