using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    public sealed class WeaponInventory
    {
        private readonly Dictionary<WeaponKind, WeaponState> states = new Dictionary<WeaponKind, WeaponState>();
        private readonly List<WeaponKind> kinds = new List<WeaponKind>();
        public IReadOnlyList<WeaponKind> Kinds { get; }

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
