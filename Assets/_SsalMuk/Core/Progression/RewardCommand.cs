using System;
namespace SsalMuk.Core
{
    public readonly struct RewardCommand
    {
        public Guid RunId { get; }
        public long OfferId { get; }
        public int Slot { get; }
        public long OwnershipVersion { get; }
        public RewardCommand(Guid runId, long offerId, int slot, long ownershipVersion)
        { RunId = runId; OfferId = offerId; Slot = slot; OwnershipVersion = ownershipVersion; }
    }
}
