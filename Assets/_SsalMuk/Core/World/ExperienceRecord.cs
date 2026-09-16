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
        public WorldPosition PreviousPosition { get; internal set; }
        public double CreatedAt { get; }
        public double AttractionStartedAt { get; internal set; }
        internal ExperienceRecord(long id, WorldPosition position, BigInteger value, double createdAt)
        { Id = id; Position = PreviousPosition = position; Value = value; State = ExperienceState.Grounded; CreatedAt = createdAt; }
    }
}
