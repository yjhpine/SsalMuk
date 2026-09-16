namespace SsalMuk.Core
{
    internal static class StableHash
    {
        internal const ulong Offset = 14695981039346656037UL;
        internal static ulong Append(ulong hash, ulong value)
        {
            unchecked { for (int i = 0; i < 8; i++) { hash = (hash ^ (byte)value) * 1099511628211UL; value >>= 8; } }
            return hash;
        }
        internal static ulong Combine(int seed, long x, long y, int version, int tag = 0)
        {
            unchecked
            {
                ulong h = Append(Offset, (uint)seed);
                h = Append(h, (ulong)x); h = Append(h, (ulong)y);
                h = Append(h, (uint)version); h = Append(h, (uint)tag);
                // Avalanche before truncating to the xorshift state or an entrance index.
                h ^= h >> 30; h *= 0xbf58476d1ce4e5b9UL;
                h ^= h >> 27; h *= 0x94d049bb133111ebUL;
                return h ^ (h >> 31);
            }
        }
    }
}
