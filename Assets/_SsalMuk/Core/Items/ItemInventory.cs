using System;
using System.Collections.Generic;
namespace SsalMuk.Core
{
    public sealed class OwnedItem
    {
        public long Id { get; }
        public ItemDefinition Definition { get; }
        internal OwnedItem(long id, ItemDefinition definition) { Id = id; Definition = definition; }
    }
    public sealed class ItemInventory
    {
        private readonly List<OwnedItem> items = new List<OwnedItem>();
        private readonly Dictionary<long, OwnedItem> byId = new Dictionary<long, OwnedItem>();
        private long nextId;
        public ItemCatalog Catalog { get; }
        public IReadOnlyList<OwnedItem> Items { get; }
        public long Version { get; private set; }
        public ItemBonuses Bonuses { get; private set; }
        internal Func<ItemBonuses, bool> CanApply { get; set; }
        public event Action Changed;
        public ItemInventory(ItemCatalog catalog) { Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog)); Items = items.AsReadOnly(); }
        public bool TryGet(long id, out OwnedItem item) => byId.TryGetValue(id, out item);
        public bool TryAdd(string id)
        {
            if (!Catalog.TryGet(id, out var definition) || nextId == long.MaxValue || Version == long.MaxValue) return false;
            ItemBonuses bonuses;
            try { bonuses = Bonuses.Plus(definition.Bonuses); } catch (ArgumentException) { return false; }
            if (CanApply != null && !CanApply(bonuses)) return false;
            Append(definition); Bonuses = bonuses; Version++; Changed?.Invoke(); return true;
        }
        internal bool CanReplace(IReadOnlyList<long> ids, ItemDefinition output, out ItemBonuses bonuses)
        {
            bonuses = default;
            if (ids == null || ids.Count != 3 || output == null || nextId == long.MaxValue || Version == long.MaxValue) return false;
            var removed = new HashSet<long>(ids);
            if (removed.Count != 3) return false;
            foreach (long id in ids) if (!byId.ContainsKey(id)) return false;
            try
            {
                foreach (var item in items) if (!removed.Contains(item.Id)) bonuses = bonuses.Plus(item.Definition.Bonuses);
                bonuses = bonuses.Plus(output.Bonuses);
            }
            catch (ArgumentException) { return false; }
            return CanApply == null || CanApply(bonuses);
        }
        internal void Replace(IReadOnlyList<long> ids, ItemDefinition output, ItemBonuses bonuses)
        {
            var removed = new HashSet<long>(ids);
            items.RemoveAll(item => removed.Contains(item.Id)); foreach (long id in ids) byId.Remove(id);
            Append(output); Bonuses = bonuses; Version++; Changed?.Invoke();
        }
        private void Append(ItemDefinition definition) { var item = new OwnedItem(++nextId, definition); items.Add(item); byId.Add(item.Id, item); }
        public void Clear() { items.Clear(); byId.Clear(); Bonuses = default; if (Version < long.MaxValue) Version++; Changed?.Invoke(); }
    }
}
