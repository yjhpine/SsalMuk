using System;
using System.Numerics;
namespace SsalMuk.Core
{
    public enum ItemRarity { Common, Uncommon, Rare, Legendary, Ultimate }
    public readonly struct ItemBonuses
    {
        public double Damage { get; }
        public double AttackSpeed { get; }
        public double MoveSpeed { get; }
        public double Regeneration { get; }
        public double Pickup { get; }
        public BigInteger ExperiencePercent { get; }
        public double LifeRatio { get; }
        public double LifeCap { get; }
        public double ChainDamage { get; }
        public int ChainCount { get; }
        public double ChainRange { get; }
        public double ExplosionDamage { get; }
        public double ExplosionRadius { get; }
        public double ShieldCapacity { get; }
        public double ShieldInterval { get; }
        public ItemBonuses(double damage = 0, double attackSpeed = 0, double moveSpeed = 0, double regeneration = 0,
            double pickup = 0, BigInteger experiencePercent = default, double lifeRatio = 0, double lifeCap = 0,
            double chainDamage = 0, int chainCount = 0, double chainRange = 0, double explosionDamage = 0,
            double explosionRadius = 0, double shieldCapacity = 0, double shieldInterval = 0)
        {
            foreach (double v in new[] { damage, attackSpeed, moveSpeed, regeneration, pickup, lifeRatio, lifeCap, chainDamage, chainRange, explosionDamage, explosionRadius, shieldCapacity, shieldInterval })
                if (v < 0 || double.IsNaN(v) || double.IsInfinity(v)) throw new ArgumentOutOfRangeException(nameof(v));
            if (experiencePercent < 0 || chainCount < 0) throw new ArgumentOutOfRangeException(nameof(experiencePercent));
            if ((lifeRatio > 0) != (lifeCap > 0) || (chainDamage > 0) != (chainCount > 0 && chainRange > 0) ||
                (explosionDamage > 0) != (explosionRadius > 0) || (shieldCapacity > 0) != (shieldInterval > 0)) throw new ArgumentException("Incomplete item effect.");
            Damage = damage; AttackSpeed = attackSpeed; MoveSpeed = moveSpeed; Regeneration = regeneration; Pickup = pickup;
            ExperiencePercent = experiencePercent; LifeRatio = lifeRatio; LifeCap = lifeCap; ChainDamage = chainDamage; ChainCount = chainCount;
            ChainRange = chainRange; ExplosionDamage = explosionDamage; ExplosionRadius = explosionRadius; ShieldCapacity = shieldCapacity; ShieldInterval = shieldInterval;
        }
        internal ItemBonuses Plus(ItemBonuses b) => new ItemBonuses(Damage + b.Damage, AttackSpeed + b.AttackSpeed, MoveSpeed + b.MoveSpeed,
            Regeneration + b.Regeneration, Pickup + b.Pickup, ExperiencePercent + b.ExperiencePercent, LifeRatio + b.LifeRatio, LifeCap + b.LifeCap,
            ChainDamage + b.ChainDamage, Math.Max(ChainCount, b.ChainCount), Math.Max(ChainRange, b.ChainRange), ExplosionDamage + b.ExplosionDamage,
            Math.Max(ExplosionRadius, b.ExplosionRadius), ShieldCapacity + b.ShieldCapacity,
            ShieldInterval == 0 ? b.ShieldInterval : b.ShieldInterval == 0 ? ShieldInterval : Math.Min(ShieldInterval, b.ShieldInterval));
    }
    public sealed class ItemDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Symbol { get; }
        public ItemRarity Rarity { get; }
        public ItemBonuses Bonuses { get; }
        public ItemDefinition(string id, string name, string symbol, ItemRarity rarity, ItemBonuses bonuses)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(symbol) || !Enum.IsDefined(typeof(ItemRarity), rarity))
                throw new ArgumentException("An item needs a stable ID, name, symbol and rarity.");
            Id = id; Name = name; Symbol = symbol; Rarity = rarity; Bonuses = bonuses;
        }
    }
}
