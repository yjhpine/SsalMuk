using System.Numerics;

namespace SsalMuk.Core
{
    public sealed class GrowthState
    {
        public BigInteger Level { get; internal set; } = BigInteger.One;
        public BigInteger ExperienceIntoLevel { get; internal set; }
        public BigInteger PendingChoices { get; internal set; }
    }
}
