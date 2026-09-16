namespace SsalMuk.Core
{
    public readonly struct SweepHit
    {
        public double Fraction { get; }
        public DVec2 Normal { get; }
        public WorldPosition Position { get; }
        public SweepHit(double fraction, DVec2 normal, WorldPosition position)
        { Fraction = fraction; Normal = normal; Position = position; }
    }
}
