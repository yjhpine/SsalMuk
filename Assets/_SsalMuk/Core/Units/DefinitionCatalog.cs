using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    public sealed class DefinitionCatalog
    {
        private readonly Dictionary<UnitKind, UnitDefinition> units = new Dictionary<UnitKind, UnitDefinition>();
        private readonly Dictionary<WeaponKind, WeaponDefinition> weapons = new Dictionary<WeaponKind, WeaponDefinition>();
        public IReadOnlyCollection<UnitDefinition> Units => units.Values;

        public DefinitionCatalog(IEnumerable<UnitDefinition> definitions, IEnumerable<WeaponDefinition> weaponDefinitions = null)
        {
            foreach (var weapon in weaponDefinitions ?? WeaponDefinition.DevelopmentPresets())
            {
                if (weapon == null || weapons.ContainsKey(weapon.Kind))
                    throw new ArgumentException("Weapon definitions must have unique kinds.", nameof(weaponDefinitions));
                weapons.Add(weapon.Kind, weapon);
            }
            foreach (WeaponKind kind in Enum.GetValues(typeof(WeaponKind)))
                if (!weapons.ContainsKey(kind)) throw new ArgumentException("Missing weapon definition: " + kind, nameof(weaponDefinitions));
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var definition in definitions)
            {
                if (definition == null || !ids.Add(definition.Id) || units.ContainsKey(definition.Kind))
                    throw new ArgumentException("Unit definitions must have unique IDs and kinds.", nameof(definitions));
                units.Add(definition.Kind, definition);
            }
            foreach (UnitKind kind in Enum.GetValues(typeof(UnitKind)))
                if (!units.ContainsKey(kind)) throw new ArgumentException("Missing unit definition: " + kind, nameof(definitions));
        }

        public UnitDefinition GetUnit(UnitKind kind) => units.TryGetValue(kind, out var definition)
            ? definition : throw new KeyNotFoundException("Missing unit definition: " + kind);
        public WeaponDefinition GetWeapon(WeaponKind kind) => weapons.TryGetValue(kind, out var definition)
            ? definition : throw new KeyNotFoundException("Missing weapon definition: " + kind);
    }
}
