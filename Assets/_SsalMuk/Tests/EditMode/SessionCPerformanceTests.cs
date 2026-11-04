using System;
using NUnit.Framework;
using SsalMuk.Core;
#if UNITY_5_3_OR_NEWER
using Unity.Profiling;
#endif

namespace SsalMuk.Tests
{
    public sealed class SessionCPerformanceTests
    {
        [TestCase(300)]
        [TestCase(500)]
        public void WarmCollectorAllocationDoesNotGrowWithNearbyExperience(int count)
        {
            using var empty = RunTestRig.Create(enableAi: false);
            using var populated = RunTestRig.Create(enableAi: false);
            // Future creation times isolate candidate lookup without collecting or moving the orbs.
            // The ordinary pickup tests cover actual attraction, contact and exactly-once payout.
            for (int i = 0; i < count; i++)
                populated.World.AddExperience(WorldPosition.FromLocal(new DVec2(.5 + i % 10 * .01, i / 10 * .01)), 1, createdAt: 1);
            Action emptyStep = () => empty.Simulation.Collector.Step(.02);
            Action populatedStep = () => populated.Simulation.Collector.Step(.02);
            for (int i = 0; i < 128; i++) { emptyStep(); populatedStep(); }
            long emptyAllocations = Allocations(emptyStep);
            long populatedAllocations = Allocations(populatedStep);
            Assert.That(populatedAllocations, Is.LessThanOrEqualTo(emptyAllocations),
                "Warm candidate queries must reuse storage instead of allocating a result collection per step.");
            Assert.That(populated.World.Experience.Count, Is.EqualTo(count));
            foreach (var orb in populated.World.Experience)
                Assert.That(orb.State, Is.EqualTo(ExperienceState.Grounded));
        }

        private static long Allocations(Action operation)
        {
#if UNITY_5_3_OR_NEWER
            // This project's Unity Mono GC byte counter is not reliable; count calibrated GC.Alloc events.
            using var recorder = new ProfilerRecorder(ProfilerCategory.Memory, "GC.Alloc", 10000,
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            Assert.That(recorder.Valid, Is.True);
            recorder.Start(); var calibration = new byte[1024]; recorder.Stop();
            Assert.That(recorder.Count, Is.GreaterThan(0));
            GC.KeepAlive(calibration);
            recorder.Reset(); recorder.Start();
            for (int i = 0; i < 32; i++) operation();
            recorder.Stop();
            Assert.That(recorder.WrappedAround, Is.False);
            long count = 0;
            for (int i = 0; i < recorder.Count; i++) count += recorder.GetSample(i).Count;
            return count;
#else
            long calibrationStart = GC.GetAllocatedBytesForCurrentThread();
            var calibration = new byte[1024];
            long calibratedBytes = GC.GetAllocatedBytesForCurrentThread() - calibrationStart;
            GC.KeepAlive(calibration);
            Assert.That(calibratedBytes, Is.GreaterThanOrEqualTo(1024));
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 32; i++) operation();
            return GC.GetAllocatedBytesForCurrentThread() - before;
#endif
        }
    }
}
