using System;
using System.Linq;
using System.Threading.Tasks;
using SsalMuk.Core;
using SsalMuk.Presentation;
using UnityEngine;
namespace SsalMuk.Unity.Diagnostics
{
    // A recorded test preset; the production catalog asset and normal GameStart are never changed.
    public sealed class DiagnosticSession : IRunBuilder, IDisposable
    {
        private readonly InitialRunBuilder initial;
        private readonly DevelopmentDefaults defaults;
        private readonly RunCoordinator coordinator;
        private readonly GameObject root;
        private readonly bool scheduled;
        private MovementSystem movement;
        private DamageService damage;
        private DeathService death;
        private bool disposed;
        public static int ActiveCount { get; private set; }
        public RunModel Run { get; }
        public RunSimulation Simulation { get; private set; }
        public WorldView View { get; }
        public WorldPresenter Presenter { get; }
        public MovementSystem Movement => movement;
        public DiagnosticSession(GameCatalog catalog, int seed, bool scheduledSpawns = true, IChunkGenerator terrain = null)
        {
            defaults = catalog.Defaults; scheduled = scheduledSpawns;
            var baseline = catalog.CreateDefinitions();
            var definitions = new DefinitionCatalog(baseline.Units.Select(d => d.Kind == UnitKind.Player || d.Kind == UnitKind.Boss ?
                new UnitDefinition("diagnostic-" + d.Kind, d.Kind, 1000000000, d.MoveSpeed, d.BodyRadius, d.ContactDamage, d.ExperienceReward, d.HurtRadius, d.VisualFootOffset) : d),
                Enum.GetValues(typeof(WeaponKind)).Cast<WeaponKind>().Select(baseline.GetWeapon));
            var streams = new SeedStreams(seed);
            Run = new RunModel(Guid.NewGuid(), seed, definitions, terrain ?? new ChunkGenerator(streams.MapSeed, defaults.CreateMapSettings()),
                growthSettings: defaults.CreateGrowthSettings(), pickupSettings: defaults.CreatePickupSettings(), rewardWeights: defaults.CreateRewardWeights());
            root = new GameObject("Diagnostic run"); View = root.AddComponent<WorldView>(); View.Initialize(catalog); Presenter = new WorldPresenter(View);
            initial = new InitialRunBuilder(Run, defaults, () => Presenter.Refresh(Run));
            ActiveCount++;
            coordinator = new RunCoordinator(new ImmediateLoader(), () => this);
            coordinator.StartRunAsync().GetAwaiter().GetResult();
            if (coordinator.Phase != RunPhase.Running) throw new InvalidOperationException(coordinator.LastError);
        }
        public void BuildWorld() => initial.BuildWorld();
        public void CreatePlayer()
        {
            initial.CreatePlayer();
            var navigation = new NavigationService(Run.World);
            movement = new MovementSystem(Run.World, navigation, Run.Player, defaults.CreateMovementSettings());
            damage = new DamageService(Run, movement); death = new DeathService(Run, damage);
            Simulation = new RunSimulation(Run, movement, new LowAiController(Run.Player, Run.World, navigation, pickupSettings: Run.PickupSettings),
                damage, death, new ContactDamageSystem(Run, movement, damage), scheduled, defaults.CreateSpawnSettings());
        }
        public void CreateInitialEnemies() { if (scheduled) initial.CreateInitialEnemies(); }
        public void Render() { initial.PrepareTerrain(); Presenter.Refresh(Run); }
        public void Dispose()
        {
            if (disposed) return; disposed = true; ActiveCount--;
            Simulation?.Dispose(); death?.Dispose(); damage?.Dispose(); movement?.Dispose(); initial.Dispose();
            if (root != null) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
            coordinator?.Dispose();
        }
        private sealed class ImmediateLoader : ISceneLoader
        { public Task LoadBattleAsync() => Task.CompletedTask; public Task LoadMainMenuAsync() => Task.CompletedTask; }
    }
}
