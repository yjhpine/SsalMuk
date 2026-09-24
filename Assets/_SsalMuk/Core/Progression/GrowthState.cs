using System.Numerics;

namespace SsalMuk.Core
{
    public sealed class GrowthState
    {
        public BigInteger Level { get; internal set; } = BigInteger.One;
        public BigInteger ExperienceIntoLevel { get; internal set; }
        public int ExperienceRemainderHundredths { get; internal set; }
        public BigInteger ExperienceHundredthsIntoLevel => ExperienceIntoLevel * 100 + ExperienceRemainderHundredths;
        public BigInteger PendingChoices { get; internal set; }
    }
}
