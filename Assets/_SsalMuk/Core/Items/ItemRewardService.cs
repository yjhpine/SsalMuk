using System;
using System.Collections.Generic;
using System.Linq;
namespace SsalMuk.Core
{
    public sealed class ItemOffer
    {
        public long Id { get; }
        public long ChestId { get; }
        public ItemRarity Rarity { get; }
        public IReadOnlyList<ItemDefinition> Choices { get; }
        internal ItemOffer(long id, long chestId, ItemRarity rarity, IEnumerable<ItemDefinition> choices)
        { Id = id; ChestId = chestId; Rarity = rarity; Choices = Array.AsReadOnly(choices.ToArray()); }
    }
    public sealed class ItemRewardService
    {
        private readonly RunModel run;
        private readonly IRandomSource random;
        private readonly Queue<ItemOffer> pending = new Queue<ItemOffer>();
        private readonly HashSet<long> opened = new HashSet<long>();
        private long lastId;
        private (long offer, int slot)? queued;
        public ItemRewardService(RunModel run)
        { this.run = run; random = new SeededRandom(unchecked((int)StableHash.Combine(run.Seed, 0, 0, 1, 1104))); }
        public ItemOffer CurrentOffer { get; private set; }
        public int PendingCount => pending.Count;
        public bool HasQueuedChoice => queued.HasValue;
        private bool Active => run.Phase == RunPhase.Running && run.Player != null && run.Player.IsAlive;
        public bool AddChest(long id, ItemRarity rarity)
        {
            if (!Active || run.IsPaused || id <= 0 || opened.Contains(id) || lastId == long.MaxValue || !Enum.IsDefined(typeof(ItemRarity), rarity)) return false;
            var pool = run.Inventory.Catalog.Pool(rarity).ToList();
            // Partial shuffle freezes distinct candidates at opening, independent of display timing.
            for (int i = 0; i < 3; i++) { int j = i + (int)(random.NextUnit() * (pool.Count - i)); var temp = pool[i]; pool[i] = pool[j]; pool[j] = temp; }
            pending.Enqueue(new ItemOffer(++lastId, id, rarity, pool.Take(3))); opened.Add(id); RefreshOffer(); return true;
        }
        public bool TryQueueChoice(Guid runId, long id, int slot)
        {
            if (!Active || run.IsPaused || queued.HasValue || run.Id != runId || CurrentOffer == null || CurrentOffer.Id != id || slot < 0 || slot >= CurrentOffer.Choices.Count) return false;
            queued = (id, slot); return true;
        }
        public void Step()
        {
            if (run.IsPaused) return;
            var command = queued; queued = null;
            if (Active && command.HasValue && CurrentOffer != null && command.Value.offer == CurrentOffer.Id &&
                run.Inventory.TryAdd(CurrentOffer.Choices[command.Value.slot].Id))
            { pending.Dequeue(); CurrentOffer = null; }
            RefreshOffer();
        }
        public void RefreshOffer()
        {
            if (!Active) { CurrentOffer = null; return; }
            if (!run.IsPaused && CurrentOffer == null && run.CurrentOffer == null && pending.Count > 0) CurrentOffer = pending.Peek();
        }
        public void Clear() { queued = null; CurrentOffer = null; pending.Clear(); opened.Clear(); }
    }
}
