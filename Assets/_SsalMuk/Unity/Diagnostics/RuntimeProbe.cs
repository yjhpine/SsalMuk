using System;
using System.Linq;
using System.Numerics;
using SsalMuk.Core;
namespace SsalMuk.Unity.Diagnostics
{
    public static class RuntimeProbe
    {
        public static RuntimeSnapshot Capture(RunModel run, RunSimulation simulation, WorldView view)
        {
            BigInteger pending = 0; long launches = 0; double delay = 0;
            foreach (var weapon in simulation.Weapons.Values)
            { pending += weapon.PendingStrikes; launches += weapon.LaunchCount; delay = Math.Max(delay, weapon.MaximumDispatchDelay); }
            return new RuntimeSnapshot { runId = run.Id.ToString("N"), phase = run.Phase.ToString(), elapsed = run.Clock.ElapsedSeconds,
                playerHealth = run.Player.Health, brain = run.Player.BrainState.ToString(), target = run.Player.TargetId?.ToString() ?? "",
                kills = run.Kills, launches = launches, units = run.Units.Count, experience = run.Experience.Count, attracting = run.World.AttractingExperienceIds.Count,
                visibleUnits = view == null ? 0 : view.VisibleUnitCount, visibleExperience = view == null ? 0 : view.VisibleExperienceCount,
                visibleAttacks = view == null ? 0 : view.VisibleAttackCount, retainedViews = view == null ? 0 : view.RetainedViewCount,
                projectiles = simulation.Projectiles.Count, pendingStrikes = pending.ToString(), maxAttackDispatchDelay = delay,
                pathRequests = simulation.Navigation.PendingRequestCount, oldestPathWaitSeconds = simulation.Navigation.OldestPendingSteps * run.Clock.FixedStep,
                spawnedNormal = simulation.Spawns?.SpawnedCount(UnitKind.Normal) ?? 0, spawnedAir = simulation.Spawns?.SpawnedCount(UnitKind.Air) ?? 0,
                spawnedBoss = simulation.Spawns?.SpawnedCount(UnitKind.Boss) ?? 0,
                pendingSpawnCount = simulation.Spawns?.Pending.Sum(x => x.Remaining) ?? 0, scopeCount = RunScope.ActiveCount + DiagnosticSession.ActiveCount };
        }
    }
}
