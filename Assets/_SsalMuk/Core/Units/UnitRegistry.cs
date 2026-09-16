using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    public sealed class UnitRegistry
    {
        private readonly Dictionary<long, UnitModel> units = new Dictionary<long, UnitModel>();
        private long lastId;
        public Guid RunId { get; }
        public int Count => units.Count;
        public IReadOnlyCollection<UnitModel> Units => units.Values;

        public UnitRegistry(Guid runId)
        {
            if (runId == Guid.Empty) throw new ArgumentException("A registry requires a run identity.", nameof(runId));
            RunId = runId;
        }

        internal void Register(UnitModel unit)
        {
            if (unit == null || unit.RunId != RunId || unit.Id != 0)
                throw new ArgumentException("Only a new unit from this run can be registered.", nameof(unit));
            long id = checked(lastId + 1);
            unit.AssignIdentity(id);
            units.Add(id, unit);
            lastId = id;
        }

        public UnitModel Get(long id) => units.TryGetValue(id, out var unit)
            ? unit : throw new KeyNotFoundException("No unit has ID " + id + ".");
        public bool TryGet(long id, out UnitModel unit) => units.TryGetValue(id, out unit);
        public bool Remove(long id) => units.Remove(id);
    }
}
