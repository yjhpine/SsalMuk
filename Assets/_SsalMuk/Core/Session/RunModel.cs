using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    public sealed class RunModel : IRunReadModel, IItemReadModel, IChestReadModel, IDisposable
    {
        public Guid Id { get; }
        public Guid RunId => Id;
        public int Seed { get; }
        public SeedStreams Streams { get; }
        public DefinitionCatalog Definitions { get; }
        public GrowthSettings GrowthSettings { get; }
        public PickupSettings PickupSettings { get; }
        public RewardService Rewards { get; }
        public ChestSystem Chests { get; }
        public ItemInventory Inventory { get; }
        public FusionService Fusion { get; }
        public ItemRewardService ItemRewards { get; }
        public bool HasItemChoice => ItemRewards.CurrentOffer != null;
        public IRunCommands Commands => Rewards;
        public OfferSnapshot CurrentOffer => Rewards.CurrentOffer;
        public WorldStore World { get; }
        public RunClock Clock { get; }
        public ICombatReadModel Combat { get; internal set; }
        public PlayerModel Player { get; private set; }
        public WeaponKind StartingWeapon { get; internal set; } = WeaponKind.Sword;
        public bool IsPaused { get; internal set; }
        public long Kills { get; internal set; }
        public RunResult Result { get; private set; }
        public event Action<RunResult> Completed;
        internal void CompleteDeath()
        {
            if (Phase != RunPhase.Running || Result != null || Player == null || Player.IsAlive) return;
            Result = new RunResult(Id, Clock.ElapsedSeconds, Kills, Player.Level);
            Player.ResetGrowth();
            Inventory.Clear(); Fusion.Clear(); ItemRewards.Clear();
            Rewards.Dispose();
            Clock.Stop(); Phase = RunPhase.Results; Completed?.Invoke(Result);
        }
        private long lastAttackId;
        public long AllocateAttackId() => lastAttackId = checked(lastAttackId + 1);
        public WeaponStats CurrentWeaponStats(WeaponKind kind)
        {
            var stats = StatCalculator.Calculate(Definitions.GetWeapon(kind), Player.Weapons.Get(kind), GrowthSettings);
            var bonuses = Inventory.Bonuses;
            return new WeaponStats(stats.Damage * (1 + bonuses.Damage), stats.Range * Player.Effects.RangeMultiplier, stats.PeriodSeconds / (1 + bonuses.AttackSpeed),
                stats.Copies, stats.Repeats, stats.BurstFraction);
        }
        public RunPhase Phase { get; internal set; } = RunPhase.MainMenu;
        public WorldPosition ViewOrigin => Player?.Position ?? new WorldPosition(default, new DVec2(16.5, 16.5));
        public IReadOnlyCollection<UnitModel> Units => World.Units.Units;
        public IReadOnlyCollection<ExperienceRecord> Experience => World.Experience;
        public IReadOnlyCollection<PowerupRecord> Items => World.Items;
        public IReadOnlyCollection<ChunkData> Terrain => World.CachedTerrain;
        public RunModel(Guid id, int seed, DefinitionCatalog definitions, IChunkGenerator generator, double fixedStep = 0.02,
            GrowthSettings growthSettings = null, PickupSettings pickupSettings = null, RewardWeights rewardWeights = null, ItemCatalog itemCatalog = null)
        {
            if (id == Guid.Empty) throw new ArgumentException("Run identity is required.", nameof(id));
            Id = id; Seed = seed; Streams = new SeedStreams(seed); Definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            GrowthSettings = growthSettings ?? new GrowthSettings();
            PickupSettings = pickupSettings ?? PickupSettings.TestDefaults();
            PickupSettings.ValidateForPlayer(Definitions.GetUnit(UnitKind.Player).BodyRadius);
            World = new WorldStore(new UnitRegistry(id), generator); Clock = new RunClock(fixedStep);
            Rewards = new RewardService(this, Streams.Reward, rewardWeights ?? RewardWeights.TestDefaults());
            Chests = new ChestSystem(this);
            Inventory = new ItemInventory(itemCatalog ?? ItemCatalog.Default()); Inventory.CanApply = CanApplyItems;
            Fusion = new FusionService(this); ItemRewards = new ItemRewardService(this);
        }
        public void SetPlayer(PlayerModel player)
        {
            if (Player != null || player == null || player.RunId != Id || !World.Units.TryGet(player.Id, out var registered) || !ReferenceEquals(registered, player))
                throw new ArgumentException("The run needs one registered player from its own world.", nameof(player));
            player.InitializeStartingWeapon(StartingWeapon); player.Inventory = Inventory; Player = player;
        }
        private bool CanApplyItems(ItemBonuses bonus)
        {
            if (Player == null) return true;
            if (!FinitePositive(Player.Definition.MoveSpeed * Player.Upgrades.SpeedMultiplier * Player.Effects.SpeedMultiplier * (1 + bonus.MoveSpeed)) ||
                !FinitePositive(PickupSettings.AttractionRadius * Player.Upgrades.PickupMultiplier * (1 + bonus.Pickup)) ||
                double.IsInfinity(Player.Upgrades.HealingPerSecond + bonus.Regeneration)) return false;
            foreach (var kind in Player.Weapons.Kinds)
            {
                var stats = StatCalculator.Calculate(Definitions.GetWeapon(kind), Player.Weapons.Get(kind), GrowthSettings);
                double damage = stats.Damage * (1 + bonus.Damage);
                if (!FinitePositive(damage) || !FinitePositive(stats.PeriodSeconds / (1 + bonus.AttackSpeed)) ||
                    double.IsInfinity(damage * bonus.ChainDamage) || double.IsInfinity(damage * bonus.ExplosionDamage)) return false;
            }
            return true;
        }
        private static bool FinitePositive(double value) => value > 0 && !double.IsInfinity(value) && !double.IsNaN(value);
        public void Dispose()
        {
            if (Phase == RunPhase.Disposed) return;
            Clock.Stop(); Phase = RunPhase.Disposed; Completed = null; Rewards.Dispose(); ItemRewards.Clear(); Fusion.Clear(); Inventory.Clear(); Player?.ResetGrowth(); World.Dispose();
        }
    }
}
