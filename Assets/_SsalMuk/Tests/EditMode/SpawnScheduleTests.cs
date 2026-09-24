using System;
using System.Linq;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class SpawnScheduleTests
    {
        [TestCase(0, 10, 5), TestCase(59.999, 10, 5), TestCase(60, 11.5, 5.8)]
        [TestCase(299.999, 16, 8.2), TestCase(300, 22.5, 11), TestCase(359.999, 22.5, 11)]
        [TestCase(360, 24, 11.8), TestCase(540, 28.5, 14.2), TestCase(600, 40, 15), TestCase(900, 62.5, 15)]
        [TestCase(1200, 90, 15), TestCase(1800, 160, 15)]
        public void NormalStatsGrowOnMinuteBoundariesWithIncreasingFiveMinuteBonuses(double seconds, double health, double damage)
        {
            var baseline = new UnitDefinition("normal", UnitKind.Normal, 10, 2.85, .325, 5, 1);
            var curve = new DifficultyCurve(new SpawnSettings()); var result = curve.AtSpawn(baseline, seconds);
            Assert.That(result.MaxHealth, Is.EqualTo(health).Within(1e-8));
            Assert.That(result.ContactDamage, Is.EqualTo(damage).Within(1e-8));
            Assert.That(result.MoveSpeed, Is.EqualTo(baseline.MoveSpeed));
            Assert.That(baseline.MaxHealth, Is.EqualTo(10)); Assert.That(baseline.ContactDamage, Is.EqualTo(5));
        }
        [TestCase(0), TestCase(300), TestCase(600), TestCase(3600)]
        public void BossContactDamageStaysSixtyWhileHealthStillScales(double seconds)
        {
            var baseline = new UnitDefinition("boss", UnitKind.Boss, 600, 3.6, 1.125, 60, 30);
            var result = new DifficultyCurve(new SpawnSettings()).AtSpawn(baseline, seconds);
            Assert.That(result.ContactDamage, Is.EqualTo(60));
            Assert.That(result.MaxHealth, Is.EqualTo(600 * (1 + seconds / 600)));
        }
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
            Assert.That(curve.NormalDeadline(270), Is.EqualTo(120).Within(1e-8));
            Assert.That(curve.NormalDeadline(720), Is.EqualTo(240).Within(1e-8));
            Assert.That(curve.NormalDeadline(2) - curve.NormalDeadline(1), Is.GreaterThan(curve.NormalDeadline(480) - curve.NormalDeadline(479)));
            Assert.That(curve.AirCount(20), Is.EqualTo(8)); Assert.That(curve.AirCount(120), Is.EqualTo(9));
            using var rig = RunTestRig.Create(); var baseline = rig.Run.Definitions.GetUnit(UnitKind.Boss); var atFive = curve.AtSpawn(baseline, 300);
            var atTen = curve.AtSpawn(baseline, 600);
            Assert.That(atFive.MaxHealth, Is.EqualTo(baseline.MaxHealth * 1.5)); Assert.That(atTen.MaxHealth, Is.EqualTo(baseline.MaxHealth * 2));
            Assert.That(atFive.ContactDamage, Is.EqualTo(baseline.ContactDamage));
            Assert.That(atTen.ContactDamage, Is.EqualTo(baseline.ContactDamage));
            Assert.That(atFive.ExperienceReward, Is.EqualTo(baseline.ExperienceReward)); Assert.That(atFive.MoveSpeed, Is.EqualTo(baseline.MoveSpeed));
            Assert.That(atFive.MaxHealth, Is.EqualTo(baseline.MaxHealth * 1.5));
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
