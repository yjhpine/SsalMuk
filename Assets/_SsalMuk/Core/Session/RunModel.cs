using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    public sealed class RunModel : IRunReadModel, IDisposable
    {
        public Guid Id { get; }
        public Guid RunId => Id;
        public int Seed { get; }
        public DefinitionCatalog Definitions { get; }
        public GrowthSettings GrowthSettings { get; }
        public PickupSettings PickupSettings { get; }
        public WorldStore World { get; }
        public RunClock Clock { get; }
        public PlayerModel Player { get; private set; }
        public long Kills { get; internal set; }
        public RunResult Result { get; private set; }
        public event Action<RunResult> Completed;
        internal void CompleteDeath()
        {
            if (Phase != RunPhase.Running || Result != null || Player == null || Player.IsAlive) return;
            Result = new RunResult(Id, Clock.ElapsedSeconds, Kills, Player.Level);
            Player.ResetGrowth();
            Clock.Stop(); Phase = RunPhase.Results; Completed?.Invoke(Result);
        }
        private long lastAttackId;
        public long AllocateAttackId() => lastAttackId = checked(lastAttackId + 1);
        public RunPhase Phase { get; internal set; } = RunPhase.MainMenu;
        public WorldPosition ViewOrigin => Player?.Position ?? new WorldPosition(default, new DVec2(16.5, 16.5));
        public IReadOnlyCollection<UnitModel> Units => World.Units.Units;
        public IReadOnlyCollection<ExperienceRecord> Experience => World.Experience;
        public IReadOnlyCollection<ChunkData> Terrain => World.CachedTerrain;
        public RunModel(Guid id, int seed, DefinitionCatalog definitions, IChunkGenerator generator, double fixedStep = 0.02,
            GrowthSettings growthSettings = null, PickupSettings pickupSettings = null)
        {
            if (id == Guid.Empty) throw new ArgumentException("Run identity is required.", nameof(id));
            Id = id; Seed = seed; Definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            GrowthSettings = growthSettings ?? new GrowthSettings();
            PickupSettings = pickupSettings ?? PickupSettings.TestDefaults();
            PickupSettings.ValidateForPlayer(Definitions.GetUnit(UnitKind.Player).BodyRadius);
            World = new WorldStore(new UnitRegistry(id), generator); Clock = new RunClock(fixedStep);
        }
        public void SetPlayer(PlayerModel player)
        {
            if (Player != null || player == null || player.RunId != Id || !World.Units.TryGet(player.Id, out var registered) || !ReferenceEquals(registered, player))
                throw new ArgumentException("The run needs one registered player from its own world.", nameof(player));
            Player = player;
        }
        public void Dispose()
        {
            if (Phase == RunPhase.Disposed) return;
            Clock.Stop(); Phase = RunPhase.Disposed; Completed = null; Player?.ResetGrowth(); World.Dispose();
        }
    }
}
