using System;
namespace SsalMuk.Unity.Diagnostics
{
    public sealed class ScenarioDefinition
    {
        public string Id { get; }
        public double Seconds { get; }
        public string Mode { get; }
        private ScenarioDefinition(string id, double seconds, string mode) { Id = id; Seconds = seconds; Mode = mode; }
        public static ScenarioDefinition Get(string id)
        {
            switch (id)
            {
                case "EndToEnd": return new ScenarioDefinition(id, 0, "Smoke");
                case "CrowdCorridor": return new ScenarioDefinition(id, 12, "Stress");
                case "PersistentWorld10m": return new ScenarioDefinition(id, 600, "Stress");
                case "PersistentWorld30m": return new ScenarioDefinition(id, 1800, "Stress");
                case "HighGrowth": return new ScenarioDefinition(id, 0, "Stress");
                case "RepeatedRestart": return new ScenarioDefinition(id, 0, "Smoke");
                default: throw new ArgumentException("Unknown scenario: " + id);
            }
        }
    }
    [Serializable]
    public sealed class ScenarioReport
    {
        public int schemaVersion = 1, seed, width, height, systemMemoryMB, errorCount;
        public string runId, sourceHash, scenario, status, failureKind, message, startedUtc, completedUtc;
        public string unityVersion, environment, processor, graphics, operatingSystem, preset, executionMode;
        public long steps;
        public double simulationSeconds;
        public string[] observations = Array.Empty<string>();
        public RuntimeSnapshot[] samples = Array.Empty<RuntimeSnapshot>();
        public MetricSummary metrics;
    }
    [Serializable]
    public sealed class RuntimeSnapshot
    {
        public string runId, phase, brain, target, pendingStrikes;
        public double elapsed, playerHealth, maxAttackDispatchDelay, oldestPathWaitSeconds;
        public long kills, launches, spawnedNormal, spawnedAir, spawnedBoss, pendingSpawnCount, departedAir;
        public int units, experience, attracting, visibleUnits, visibleExperience, visibleAttacks, retainedViews, projectiles, pathRequests, scopeCount;
    }
    [Serializable]
    public sealed class MetricSummary
    {
        public string allocationSource, memorySource;
        public long tickCount, renderCount, allocatedBytes, peakManagedBytes, peakUnityBytes, peakWorkingSetBytes;
        public double modelMeanMs, modelP95Ms, modelMaxMs, viewMeanMs, viewP95Ms, frameWallP95Ms, wallSeconds;
    }
}
