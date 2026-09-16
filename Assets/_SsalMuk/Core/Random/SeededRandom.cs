namespace SsalMuk.Core
{
    public sealed class SeededRandom : IRandomSource
    {
        private uint state;
        public SeededRandom(int seed) { state = unchecked((uint)seed); if (state == 0) state = 0x6D2B79F5u; }
        public double NextUnit()
        {
            uint x = state; x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            state = x; return x / 4294967296.0;
        }
    }
}
