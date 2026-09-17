using System;

namespace SsalMuk.Core
{
    public enum FurnitureKind { Bookshelf, Desk, Chair }

    // Immutable static footprint. The taller, leaning sprite does not change collision geometry.
    public readonly struct FurniturePlacement
    {
        public FurnitureKind Kind { get; }
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
        public int Variation { get; }
        public FurniturePlacement(FurnitureKind kind, int x, int y, int width, int height, int variation)
        {
            if (!Enum.IsDefined(typeof(FurnitureKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (x < 0 || y < 0 || width < 1 || height < 1 || x > ChunkData.Size - width || y > ChunkData.Size - height)
                throw new ArgumentOutOfRangeException(nameof(x));
            if (variation < 0 || variation > 1) throw new ArgumentOutOfRangeException(nameof(variation));
            Kind = kind; X = x; Y = y; Width = width; Height = height; Variation = variation;
        }
    }
}
