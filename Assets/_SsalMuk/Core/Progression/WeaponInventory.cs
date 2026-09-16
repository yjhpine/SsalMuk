using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    public sealed class WeaponInventory
    {
        private readonly Dictionary<WeaponKind, WeaponState> states = new Dictionary<WeaponKind, WeaponState>();
        private readonly List<WeaponKind> kinds = new List<WeaponKind>();
        public IReadOnlyList<WeaponKind> Kinds { get; }
        public long OwnershipVersion { get; private set; }
        public bool Owns(WeaponKind kind) => states.ContainsKey(kind);
        internal void Equip(WeaponKind kind)
        {
            if (!Enum.IsDefined(typeof(WeaponKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (Owns(kind)) throw new InvalidOperationException("This weapon is already owned.");
            long next = checked(OwnershipVersion + 1);
            states.Add(kind, new WeaponState(kind)); kinds.Add(kind); OwnershipVersion = next;
        }

        internal WeaponInventory()
        {
            states.Add(WeaponKind.Sword, new WeaponState(WeaponKind.Sword));
            kinds.Add(WeaponKind.Sword);
            Kinds = kinds.AsReadOnly();
        }

        public WeaponState Get(WeaponKind kind) => states.TryGetValue(kind, out var state)
            ? state : throw new KeyNotFoundException("This unit does not own " + kind + ".");
    }
}
