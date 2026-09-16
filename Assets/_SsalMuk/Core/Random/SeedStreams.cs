namespace SsalMuk.Core
{
    public sealed class SeedStreams
    {
        public int MapSeed { get; }
        public int SpawnSeed { get; }
        public int RewardSeed { get; }
        public IRandomSource Spawn { get; }
        public IRandomSource Reward { get; }
        public SeedStreams(int seed)
        {
            MapSeed = unchecked((int)StableHash.Combine(seed, 0x4D4150, 0, 1));
            SpawnSeed = unchecked((int)StableHash.Combine(seed, 0x535041574E, 0, 1));
            RewardSeed = unchecked((int)StableHash.Combine(seed, 0x524557415244, 0, 1));
            Spawn = new SeededRandom(SpawnSeed); Reward = new SeededRandom(RewardSeed);
        }
    }
}
