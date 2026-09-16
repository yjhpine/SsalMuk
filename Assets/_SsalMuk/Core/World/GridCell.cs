using System;

namespace SsalMuk.Core
{
    public readonly struct GridCell : IEquatable<GridCell>
    {
        public ChunkCoord Chunk { get; }
        public int X { get; }
        public int Y { get; }
        public WorldPosition Center => new WorldPosition(Chunk, new DVec2(X + 0.5, Y + 0.5));
        public GridCell(ChunkCoord chunk, int x, int y)
        {
            if (x < 0 || x >= ChunkData.Size || y < 0 || y >= ChunkData.Size) throw new ArgumentOutOfRangeException(nameof(x));
            Chunk = chunk; X = x; Y = y;
        }
        public static GridCell At(WorldPosition position) => new GridCell(position.Chunk, (int)Math.Floor(position.Local.X), (int)Math.Floor(position.Local.Y));
        public GridCell Offset(int x, int y) => At(Center.Offset(new DVec2(x, y)));
        public bool Equals(GridCell other) => Chunk.Equals(other.Chunk) && X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridCell cell && Equals(cell);
        public override int GetHashCode() => unchecked((Chunk.GetHashCode() * 397 ^ X) * 397 ^ Y);
    }
}
