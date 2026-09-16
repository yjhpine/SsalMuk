using System;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class RunClockTests
    {
        [Test]
        public void LoadingDoesNotConsumeSurvivalTime()
        {
            var clock = new RunClock(0.02);
            clock.Advance(15000);
            Assert.That(clock.ElapsedSeconds, Is.Zero);
            clock.Start();
            clock.Advance(15000);
            Assert.That(clock.ElapsedSeconds, Is.EqualTo(300).Within(1e-9));
        }

        [Test]
        public void StopFreezesTimeAndStartResumesTheSameRun()
        {
            var clock = new RunClock(0.02);
            clock.Start(); clock.Start(); clock.Advance(10);
            clock.Stop(); clock.Advance(100);
            Assert.That(clock.ElapsedTicks, Is.EqualTo(10));
            clock.Start(); clock.Advance(5);
            Assert.That(clock.ElapsedSeconds, Is.EqualTo(0.3).Within(1e-9));
        }

        [TestCase(0)] [TestCase(-0.02)] [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)]
        public void InvalidFixedStepIsRejected(double step)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RunClock(step));
        }

        [Test]
        public void NegativeTicksCannotMoveTimeBackwards()
        {
            var clock = new RunClock(0.02);
            clock.Start(); clock.Advance();
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(-1));
            Assert.That(clock.ElapsedTicks, Is.EqualTo(1));
        }
    }
}
