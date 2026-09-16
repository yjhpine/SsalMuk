using System;
using System.Linq;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class SpawnScheduleTests
    {
        [Test]
        public void BossDeadlinesAreNeverSkippedOrRepeatedAcrossLongUpdates()
        {
            var schedule = new BossSchedule(300);
            Assert.That(schedule.CollectDueTimes(299.9), Is.Empty);
            CollectionAssert.AreEqual(new[] { 300d, 600d, 900d }, schedule.CollectDueTimes(900));
            Assert.That(schedule.CollectDueTimes(900), Is.Empty);
            CollectionAssert.AreEqual(new[] { 1200d }, schedule.CollectDueTimes(1200));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BossSchedule(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => schedule.CollectDueTimes(double.NaN));
        }
        [Test]
        public void SpawnRateIntegratesTheRisingRateAndDifficultyDoesNotMutateOldDefinitions()
        {
            var curve = new DifficultyCurve(new SpawnSettings());
            Assert.That(curve.NormalDeadline(180), Is.EqualTo(120).Within(1e-8));
            Assert.That(curve.NormalDeadline(480), Is.EqualTo(240).Within(1e-8));
            Assert.That(curve.NormalDeadline(2) - curve.NormalDeadline(1), Is.GreaterThan(curve.NormalDeadline(480) - curve.NormalDeadline(479)));
            Assert.That(curve.AirCount(20), Is.EqualTo(8)); Assert.That(curve.AirCount(120), Is.EqualTo(9));
            using var rig = RunTestRig.Create(); var baseline = rig.Run.Definitions.GetUnit(UnitKind.Boss); var atFive = curve.AtSpawn(baseline, 300);
            var atTen = curve.AtSpawn(baseline, 600);
            Assert.That(atFive.MaxHealth, Is.EqualTo(baseline.MaxHealth * 2)); Assert.That(atTen.MaxHealth, Is.EqualTo(baseline.MaxHealth * 3));
            Assert.That(atFive.ContactDamage, Is.EqualTo(baseline.ContactDamage * 2));
            Assert.That(atFive.ExperienceReward, Is.EqualTo(baseline.ExperienceReward)); Assert.That(atFive.MoveSpeed, Is.EqualTo(baseline.MoveSpeed));
            Assert.That(atFive.MaxHealth, Is.EqualTo(baseline.MaxHealth * 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpawnSettings(creationBudget: 0));
        }
        [Test]
        public void CameraBoundsUseRelativeCoordinatesAtVeryLargeAndNegativeChunks()
        {
            var center = new WorldPosition(new ChunkCoord(9007199254740993, -40000), new DVec2(31.5, 0.5));
            var view = new WorldRect(center, 16, 9);
            Assert.That(view.Contains(center.Offset(new DVec2(16, -9))), Is.True);
            Assert.That(view.Contains(center.Offset(new DVec2(16.01, 0))), Is.False);
            Assert.That(view.Contains(center.Offset(new DVec2(16.5, 0)), 0.6), Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() => new WorldRect(center, 0, 9));
        }
    }
}
