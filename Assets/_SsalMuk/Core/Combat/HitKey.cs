using System;
using System.Numerics;

namespace SsalMuk.Core
{
    public readonly struct HitKey : IEquatable<HitKey>
    {
        public Guid RunId { get; }
        public long AttackId { get; }
        public BigInteger Copy { get; }
        public BigInteger Repeat { get; }
        public HitKey(Guid runId, long attackId, BigInteger copy, BigInteger repeat)
        { RunId = runId; AttackId = attackId; Copy = copy; Repeat = repeat; }
        public bool Equals(HitKey other) => RunId == other.RunId && AttackId == other.AttackId && Copy == other.Copy && Repeat == other.Repeat;
        public override bool Equals(object obj) => obj is HitKey other && Equals(other);
        public override int GetHashCode() => unchecked(((RunId.GetHashCode() * 397 ^ AttackId.GetHashCode()) * 397 ^ Copy.GetHashCode()) * 397 ^ Repeat.GetHashCode());
    }
}
