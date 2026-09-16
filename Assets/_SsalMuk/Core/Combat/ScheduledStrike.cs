using System.Numerics;
namespace SsalMuk.Core
{
    public readonly struct ScheduledStrike
    {
        public double Time { get; }
        public BigInteger Repeat { get; }
        public ScheduledStrike(double time, BigInteger repeat) { Time = time; Repeat = repeat; }
    }
}
