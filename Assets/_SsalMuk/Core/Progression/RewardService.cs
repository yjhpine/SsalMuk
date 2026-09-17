using System;
namespace SsalMuk.Core
{
    public sealed class RewardService : IRunCommands, IDisposable
    {
        private readonly RunModel run;
        private readonly IRandomSource random;
        private readonly RewardWeights weights;
        private readonly OfferGenerator generator = new OfferGenerator();
        private readonly GrowthService growth;
        private RewardCommand? queued;
        private long lastOfferId;
        private bool disposed;
        public OfferSnapshot CurrentOffer { get; private set; }
        public bool HasQueuedChoice => queued.HasValue;
        public RewardService(RunModel run, IRandomSource random, RewardWeights weights)
        {
            this.run = run ?? throw new ArgumentNullException(nameof(run));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            this.weights = weights ?? throw new ArgumentNullException(nameof(weights)); growth = new GrowthService(run);
        }
        private bool IsActive => !disposed && run.Phase == RunPhase.Running && run.Player != null && run.Player.IsAlive;
        private bool IsValid(RewardCommand command) => IsActive && CurrentOffer != null && run.Player.Growth.PendingChoices > 0 &&
            command.RunId == run.Id && command.OfferId == CurrentOffer.Id && command.Slot >= 0 && command.Slot < CurrentOffer.Choices.Count &&
            command.OwnershipVersion == CurrentOffer.OwnershipVersion && command.OwnershipVersion == run.Player.Weapons.OwnershipVersion &&
            IsAvailable(CurrentOffer.Choices[command.Slot]);
        private bool IsAvailable(RewardId reward) => reward.IsUpgrade ?
            run.Player.Weapons.Owns(reward.Weapon) && run.Player.Weapons.Get(reward.Weapon).CanUpgrade(reward.UpgradeKind) :
            !run.Player.Weapons.Owns(reward.Weapon);
        private bool OfferStillAvailable()
        {
            if (CurrentOffer == null || CurrentOffer.OwnershipVersion != run.Player.Weapons.OwnershipVersion) return false;
            foreach (var reward in CurrentOffer.Choices) if (!IsAvailable(reward)) return false;
            return true;
        }
        public bool TryQueueChoice(Guid runId, long offerId, int slot)
        {
            if (queued.HasValue || CurrentOffer == null) return false;
            var command = new RewardCommand(runId, offerId, slot, CurrentOffer.OwnershipVersion);
            if (!IsValid(command)) return false; queued = command; return true;
        }
        public void Step()
        {
            var command = queued; queued = null;
            if (command.HasValue && IsValid(command.Value))
            {
                var reward = CurrentOffer.Choices[command.Value.Slot];
                if (reward.IsUpgrade) growth.Upgrade(reward.Weapon, reward.UpgradeKind); else growth.Equip(reward.Weapon);
                run.Player.Growth.PendingChoices--; CurrentOffer = null;
            }
            RefreshOffer();
        }
        public void RefreshOffer()
        {
            if (!IsActive || run.Player.Growth.PendingChoices <= 0) { CurrentOffer = null; return; }
            if (OfferStillAvailable()) return;
            long next = checked(lastOfferId + 1);
            CurrentOffer = new OfferSnapshot(next, run.Player.Weapons.OwnershipVersion,
                generator.Generate(run.Player.Weapons.Kinds, random, weights, (weapon, upgrade) => run.Player.Weapons.Get(weapon).CanUpgrade(upgrade)));
            lastOfferId = next;
        }
        public void Dispose() { disposed = true; CurrentOffer = null; queued = null; }
    }
}
