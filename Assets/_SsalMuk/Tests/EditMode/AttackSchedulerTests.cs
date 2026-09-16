using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class AttackSchedulerTests
    {
        [Test]
        public void LongFrameKeepsEveryDeadlineAndAnEmptyWorldDoesNotAccumulateAttacks()
        {
            var scheduler = new AttackScheduler();
            var times = scheduler.CollectDueTimes(0, 0.35, 0.1, true).ToArray();
            Assert.That(times.Length, Is.EqualTo(4));
            for (int i = 0; i < 4; i++) Assert.That(times[i], Is.EqualTo(i * 0.1).Within(1e-10));
            Assert.That(scheduler.CollectDueTimes(0.35, 100, 0.1, false), Is.Empty);
            Assert.That(scheduler.CollectDueTimes(100, 100.01, 0.1, true).ToArray(), Is.EqualTo(new[] { 100d }));
        }

        [Test]
        public void InvalidOrUnrepresentablePeriodsFailInsteadOfLoopingOrDroppingShots()
        {
            var scheduler = new AttackScheduler();
            Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.CollectDueTimes(0, 1, 0, true).ToArray());
            Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.CollectDueTimes(0, 1, double.NaN, true).ToArray());
            Assert.Throws<OverflowException>(() => scheduler.CollectDueTimes(1e20, 1e20 + 1e6, 1, true).ToArray());
        }

        [Test]
        public void ALaunchedSwingKeepsStatsAndDirectionWhenTheTargetDies()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            long first = rig.Spawn(UnitKind.Normal, new DVec2(1, 0), 100);
            WeaponStats current = new WeaponStats(8, 1.6, 1); var launched = new List<AttackInstance>();
            var runtime = new WeaponRuntime(rig.Run, WeaponKind.Sword, rig.Movement, rig.Damage, () => current);
            runtime.Launched += launched.Add; runtime.Tick(0, 0.02);
            Assert.That(launched.Count, Is.EqualTo(1));
            rig.Hit(first, 1000); rig.Death.Flush();
            long behind = rig.Spawn(UnitKind.Normal, new DVec2(-0.8, 0), 100);
            long front = rig.Spawn(UnitKind.Normal, new DVec2(1, 0), 100);
            current = new WeaponStats(80, 8, 0.1); runtime.Tick(0.02, 0.3);
            Assert.That(rig.Unit(front).Health, Is.EqualTo(92)); Assert.That(rig.Unit(behind).Health, Is.EqualTo(100));
        }
    }
}
