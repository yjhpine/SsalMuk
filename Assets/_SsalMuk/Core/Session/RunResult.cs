using System;
using System.Numerics;

namespace SsalMuk.Core
{
    public sealed class RunResult
    {
        public Guid RunId { get; }
        public double SurvivalSeconds { get; }
        public long KillCount { get; }
        public BigInteger FinalLevel { get; }
        public RunResult(Guid runId, double survivalSeconds, long killCount, BigInteger finalLevel)
        { RunId = runId; SurvivalSeconds = survivalSeconds; KillCount = killCount; FinalLevel = finalLevel; }
    }
}
