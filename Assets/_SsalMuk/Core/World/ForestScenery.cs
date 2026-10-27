using System;

namespace SsalMuk.Core
{
    public enum ForestPropKind { Tree, Rock, Grass }
    public readonly struct ForestScenery
    {
        public ForestPropKind Kind { get; }
        public int X { get; }
        public int Y { get; }
        public int Variation { get; }
        public bool BlocksMovement => Kind != ForestPropKind.Grass;
        public ForestScenery(ForestPropKind kind, int x, int y, int variation)
        {
            if (!Enum.IsDefined(typeof(ForestPropKind), kind) || variation < 0 || variation > 1 ||
                x < 0 || y < 0 || x >= 32 || y >= 32 || kind != ForestPropKind.Grass && (x > 30 || y > 30))
                throw new ArgumentOutOfRangeException(nameof(x));
            Kind = kind; X = x; Y = y; Variation = variation;
        }
    }
    public readonly struct ForestClearing
    {
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
        public ForestClearing(int x, int y, int width, int height)
        { X = x; Y = y; Width = width; Height = height; }
    }
}
