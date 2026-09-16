using System;
namespace SsalMuk.Presentation
{
    public readonly struct WalkPose
    {
        public double RotationDegrees { get; }
        public double Height { get; }
        public WalkPose(double rotation, double height) { RotationDegrees = rotation; Height = height; }
        public static WalkPose Sample(double phase, double angle, double height)
        {
            if (double.IsNaN(phase) || double.IsInfinity(phase) || angle < 0 || height < 0 ||
                double.IsNaN(angle) || double.IsInfinity(angle) || double.IsNaN(height) || double.IsInfinity(height)) throw new ArgumentOutOfRangeException(nameof(phase));
            double lean = Math.Sin(phase); return new WalkPose(angle * lean, height * Math.Abs(lean));
        }
    }
}
