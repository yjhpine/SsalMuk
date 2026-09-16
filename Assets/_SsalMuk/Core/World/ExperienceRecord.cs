using System.Numerics;

namespace SsalMuk.Core
{
    public enum ExperienceState { Grounded, Attracting, Collected }

    public sealed class ExperienceRecord
    {
        public long Id { get; }
        public WorldPosition Position { get; internal set; }
        public BigInteger Value { get; }
        public ExperienceState State { get; internal set; }
        internal ExperienceRecord(long id, WorldPosition position, BigInteger value)
        { Id = id; Position = position; Value = value; State = ExperienceState.Grounded; }
    }
}
