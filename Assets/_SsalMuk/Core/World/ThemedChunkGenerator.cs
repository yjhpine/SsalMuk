using System;

namespace SsalMuk.Core
{
    public sealed class ThemedChunkGenerator : IChunkGenerator
    {
        private readonly IChunkGenerator generator;
        public MapKind Map { get; }
        public ThemedChunkGenerator(int seed, MapSettings settings, MapKind map)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (!Enum.IsDefined(typeof(MapKind), map)) throw new ArgumentOutOfRangeException(nameof(map));
            Map = map;
            generator = map == MapKind.Forest ? (IChunkGenerator)new ForestChunkGenerator(seed, settings) : new LibraryChunkGenerator(seed, settings);
        }
        public ChunkData Generate(ChunkCoord coord) => generator.Generate(coord);
    }
}
