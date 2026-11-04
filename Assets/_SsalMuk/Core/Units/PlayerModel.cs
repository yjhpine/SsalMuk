using System.Numerics;

namespace SsalMuk.Core
{
    public sealed class PlayerModel : UnitModel
    {
        private WeaponKind startingWeapon = WeaponKind.Sword;
        public WeaponInventory Weapons { get; private set; } = new WeaponInventory();
        public GrowthState Growth { get; private set; } = new GrowthState();
        public SharedUpgradeState Upgrades { get; private set; } = new SharedUpgradeState();
        public PlayerEffects Effects { get; private set; } = new PlayerEffects();
        public ItemInventory Inventory { get; internal set; }
        public double Shield { get; internal set; }
        public ItemBonuses ItemBonuses => Inventory?.Bonuses ?? default;
        public override double MoveSpeed => Definition.MoveSpeed * Upgrades.SpeedMultiplier * Effects.SpeedMultiplier * (1 + ItemBonuses.MoveSpeed);
        public double PickupMultiplier => Upgrades.PickupMultiplier * (1 + ItemBonuses.Pickup);
        public double HealingPerSecond => Upgrades.HealingPerSecond + ItemBonuses.Regeneration;
        public BigInteger ExperienceHundredthsFor(BigInteger raw) => Upgrades.ExperienceHundredthsFor(raw) + raw * ItemBonuses.ExperiencePercent;
        public BigInteger Level => Growth.Level;
        public BrainState BrainState { get; internal set; }
        public long? TargetId { get; internal set; }
        public long? CollectionTargetId { get; internal set; }
        public DVec2 BreakoutDirection { get; internal set; }
        internal PlayerModel(UnitSpawnRequest request) : base(request) { }
        internal void InitializeStartingWeapon(WeaponKind kind)
        { Weapons = new WeaponInventory(kind); startingWeapon = kind; }
        internal void ResetGrowth()
        { Growth = new GrowthState(); Weapons = new WeaponInventory(startingWeapon); Upgrades = new SharedUpgradeState(); Effects = new PlayerEffects(); InvulnerableUntil = 0; Shield = 0; }
    }
}
