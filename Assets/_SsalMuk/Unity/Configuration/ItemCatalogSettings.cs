using System;
using System.Linq;
using SsalMuk.Core;
using UnityEngine;
namespace SsalMuk.Unity
{
    [Serializable]
    public sealed class ItemCatalogSettings
    {
        [SerializeField] private ItemEntry[] items = ItemCatalog.Default().Definitions.Select(d => new ItemEntry(d)).ToArray();
        [SerializeField] private RecipeEntry[] recipes = ItemCatalog.Default().Recipes.Select(r => new RecipeEntry(r)).ToArray();
        [SerializeField] private double[] rarityWeights = { 65, 25, 8, 1.8, .2 };
        public ItemCatalog CreateCatalog()
        {
            if (items == null || recipes == null || rarityWeights == null) throw new ArgumentException("Item catalog data is missing.");
            return new ItemCatalog(items.Select(i => i.Create()), recipes.Select(r => r.Create()), rarityWeights);
        }
        [Serializable]
        private sealed class ItemEntry
        {
            [SerializeField] private string id, displayName, symbol;
            [SerializeField] private ItemRarity rarity;
            [SerializeField] private double damage, attackSpeed, moveSpeed, regeneration, pickup;
            [SerializeField] private int experiencePercent;
            [SerializeField] private double lifeRatio, lifeCap, chainDamage, chainRange, explosionDamage, explosionRadius, shieldCapacity, shieldInterval;
            [SerializeField] private int chainCount;
            public ItemEntry(ItemDefinition d)
            {
                id = d.Id; displayName = d.Name; symbol = d.Symbol; rarity = d.Rarity; var b = d.Bonuses;
                damage = b.Damage; attackSpeed = b.AttackSpeed; moveSpeed = b.MoveSpeed; regeneration = b.Regeneration; pickup = b.Pickup;
                experiencePercent = (int)b.ExperiencePercent; lifeRatio = b.LifeRatio; lifeCap = b.LifeCap; chainDamage = b.ChainDamage;
                chainCount = b.ChainCount; chainRange = b.ChainRange; explosionDamage = b.ExplosionDamage; explosionRadius = b.ExplosionRadius;
                shieldCapacity = b.ShieldCapacity; shieldInterval = b.ShieldInterval;
            }
            public ItemDefinition Create() => new ItemDefinition(id, displayName, symbol, rarity, new ItemBonuses(damage, attackSpeed, moveSpeed,
                regeneration, pickup, experiencePercent, lifeRatio, lifeCap, chainDamage, chainCount, chainRange, explosionDamage, explosionRadius, shieldCapacity, shieldInterval));
        }
        [Serializable]
        private sealed class RecipeEntry
        {
            [SerializeField] private string id, resultId;
            [SerializeField] private string[] materials;
            public RecipeEntry(ItemRecipe r) { id = r.Id; resultId = r.ResultId; materials = r.Materials.ToArray(); }
            public ItemRecipe Create() => new ItemRecipe(id, materials, resultId);
        }
    }
}
