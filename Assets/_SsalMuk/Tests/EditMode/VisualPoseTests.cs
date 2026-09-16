using System;
using NUnit.Framework;
using SsalMuk.Presentation;

namespace SsalMuk.Tests
{
    public sealed class VisualPoseTests
    {
        [Test]
        public void WalkingHasTwoAlternatingHopsAndReturnsToTheFloorBetweenThem()
        {
            var start = WalkPose.Sample(0, 8, 0.08); Assert.That(start.RotationDegrees, Is.Zero); Assert.That(start.Height, Is.Zero);
            var left = WalkPose.Sample(Math.PI / 2, 8, 0.08);
            Assert.That(left.RotationDegrees, Is.EqualTo(8).Within(1e-8)); Assert.That(left.Height, Is.EqualTo(0.08).Within(1e-8));
            var middle = WalkPose.Sample(Math.PI, 8, 0.08); Assert.That(middle.Height, Is.Zero.Within(1e-8));
            var right = WalkPose.Sample(Math.PI * 1.5, 8, 0.08);
            Assert.That(right.RotationDegrees, Is.EqualTo(-8).Within(1e-8)); Assert.That(right.Height, Is.EqualTo(0.08).Within(1e-8));
            Assert.That(WalkPose.Sample(Math.PI * 2, 8, 0.08).Height, Is.Zero.Within(1e-8));
        }
    }
}
