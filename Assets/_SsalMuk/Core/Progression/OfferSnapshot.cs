using System;
using System.Collections.Generic;
namespace SsalMuk.Core
{
    public sealed class OfferSnapshot
    {
        public long Id { get; }
        public long OwnershipVersion { get; }
        public IReadOnlyList<RewardId> Choices { get; }
        public OfferSnapshot(long id, long ownershipVersion, IReadOnlyList<RewardId> choices)
        {
            if (id <= 0 || ownershipVersion < 0) throw new ArgumentOutOfRangeException(nameof(id));
            if (choices == null || choices.Count != 3 || new HashSet<RewardId>(choices).Count != 3) throw new ArgumentException("An offer needs three distinct rewards.");
            Id = id; OwnershipVersion = ownershipVersion; Choices = new List<RewardId>(choices).AsReadOnly();
        }
    }
}
