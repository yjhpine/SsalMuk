using System;
namespace SsalMuk.Unity
{
    public readonly struct LeaseToken : IEquatable<LeaseToken>
    {
        public Guid RunId { get; }
        public long EntityId { get; }
        public long Generation { get; }
        public LeaseToken(Guid runId, long entityId, long generation) { RunId = runId; EntityId = entityId; Generation = generation; }
        public bool Equals(LeaseToken other) => RunId == other.RunId && EntityId == other.EntityId && Generation == other.Generation;
        public override bool Equals(object other) => other is LeaseToken token && Equals(token);
        public override int GetHashCode() => RunId.GetHashCode() ^ EntityId.GetHashCode() ^ Generation.GetHashCode();
    }
}
