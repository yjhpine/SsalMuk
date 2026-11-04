using System;
using System.Collections.Generic;
using System.Linq;
namespace SsalMuk.Core
{
    public sealed class ItemRecipe
    {
        public string Id { get; }
        public IReadOnlyList<string> Materials { get; }
        public string ResultId { get; }
        internal string Key => Signature(Materials);
        public ItemRecipe(string id, IEnumerable<string> materials, string resultId)
        {
            if (string.IsNullOrWhiteSpace(id) || materials == null || string.IsNullOrWhiteSpace(resultId)) throw new ArgumentException("Invalid recipe.");
            var copy = materials.ToArray();
            if (copy.Length != 3 || copy.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("A recipe consumes three item definitions.");
            Id = id; Materials = Array.AsReadOnly(copy); ResultId = resultId;
        }
        internal static string Signature(IEnumerable<string> ids) => string.Join("|", ids.OrderBy(id => id, StringComparer.Ordinal));
    }
    public sealed class ItemCatalog
    {
        private readonly Dictionary<string, ItemDefinition> byId = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, ItemRecipe> byMaterials = new Dictionary<string, ItemRecipe>(StringComparer.Ordinal);
        private readonly IReadOnlyList<ItemDefinition>[] pools = new IReadOnlyList<ItemDefinition>[5];
        private readonly double[] weights;
        public IReadOnlyList<ItemDefinition> Definitions { get; }
        public IReadOnlyList<ItemRecipe> Recipes { get; }
        public ItemCatalog(IEnumerable<ItemDefinition> definitions, IEnumerable<ItemRecipe> recipes, IEnumerable<double> rarityWeights = null)
        {
            if (definitions == null || recipes == null) throw new ArgumentNullException(nameof(definitions));
            var defs = definitions.ToArray(); var rules = recipes.ToArray();
            foreach (var item in defs)
            {
                if (item == null || item.Id.Contains("|") || byId.ContainsKey(item.Id)) throw new ArgumentException("Duplicate or invalid item ID.");
                byId.Add(item.Id, item);
            }
            for (int i = 0; i < pools.Length; i++)
            {
                var pool = defs.Where(d => (int)d.Rarity == i).ToArray();
                if (pool.Length < 3) throw new ArgumentException("Each rarity needs at least three distinct choices.");
                pools[i] = Array.AsReadOnly(pool);
            }
            var recipeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var recipe in rules)
            {
                if (recipe == null || !recipeIds.Add(recipe.Id) || byMaterials.ContainsKey(recipe.Key) || !byId.ContainsKey(recipe.ResultId) || recipe.Materials.Any(id => !byId.ContainsKey(id)))
                    throw new ArgumentException("Invalid, duplicate or unresolved recipe.");
                var rarity = Get(recipe.Materials[0]).Rarity;
                if (rarity == ItemRarity.Ultimate || recipe.Materials.Any(id => Get(id).Rarity != rarity) || (int)Get(recipe.ResultId).Rarity != (int)rarity + 1)
                    throw new ArgumentException("Recipes consume one rarity and produce the next rarity.");
                byMaterials.Add(recipe.Key, recipe);
            }
            weights = (rarityWeights ?? new[] { 65.0, 25, 8, 1.8, .2 }).ToArray();
            if (weights.Length != 5 || weights.Any(w => w <= 0 || double.IsInfinity(w) || double.IsNaN(w)) || double.IsInfinity(weights.Sum())) throw new ArgumentException("Five positive finite rarity weights are required.");
            Definitions = Array.AsReadOnly(defs); Recipes = Array.AsReadOnly(rules);
        }
        public ItemDefinition Get(string id) => byId.TryGetValue(id, out var result) ? result : throw new ArgumentException("Unknown item: " + id);
        public bool TryGet(string id, out ItemDefinition result) { result = null; return id != null && byId.TryGetValue(id, out result); }
        public IReadOnlyList<ItemDefinition> Pool(ItemRarity rarity) => pools[(int)rarity];
        public ItemRecipe FindRecipe(IEnumerable<string> ids) => byMaterials.TryGetValue(ItemRecipe.Signature(ids), out var recipe) ? recipe : null;
        public ItemRarity RollRarity(double sample)
        {
            if (sample < 0 || sample >= 1 || double.IsNaN(sample)) throw new ArgumentOutOfRangeException(nameof(sample));
            double value = sample * weights.Sum();
            for (int i = 0; i < 4; i++) { if (value < weights[i]) return (ItemRarity)i; value -= weights[i]; }
            return ItemRarity.Ultimate;
        }
        public static ItemCatalog Default() => ItemDefaults.Create();
    }
}
