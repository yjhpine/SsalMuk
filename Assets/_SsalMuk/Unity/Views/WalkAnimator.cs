using System;
using SsalMuk.Core;
using SsalMuk.Presentation;
namespace SsalMuk.Unity
{
    public sealed class WalkAnimator
    {
        private double phase;
        public WalkPose Pose { get; private set; }
        public void Step(DVec2 input, double dt, VisualCatalog catalog)
        {
            if (dt < 0 || double.IsNaN(dt) || double.IsInfinity(dt)) throw new ArgumentOutOfRangeException(nameof(dt));
            if (input.Length > 1e-6)
            {
                phase = (phase + dt * Math.PI * 2 * catalog.WalkCycles) % (Math.PI * 2);
                Pose = WalkPose.Sample(phase, catalog.WalkAngle, catalog.WalkHeight);
            }
            else
            {
                phase = 0; double decay = Math.Exp(-dt * 35);
                double angle = Pose.RotationDegrees * decay, height = Pose.Height * decay;
                Pose = new WalkPose(Math.Abs(angle) < 0.01 ? 0 : angle, height < 0.0001 ? 0 : height);
            }
        }
        public void Reset() { phase = 0; Pose = default; }
    }
}
