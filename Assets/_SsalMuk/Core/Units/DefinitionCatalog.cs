using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    public sealed class DefinitionCatalog
    {
        private readonly Dictionary<UnitKind, UnitDefinition> units = new Dictionary<UnitKind, UnitDefinition>();
        public IReadOnlyCollection<UnitDefinition> Units => units.Values;

        public DefinitionCatalog(IEnumerable<UnitDefinition> definitions)
        {
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
    }
}
