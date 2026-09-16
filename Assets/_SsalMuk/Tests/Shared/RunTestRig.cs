using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class RunTestRig : IDisposable
    {
        private readonly RunCoordinator coordinator;
        private RigBuilder scope;
        public RunCoordinator Coordinator => coordinator;
        public RunModel Run => scope.Run;
        public WorldStore World => Run.World;
        public PlayerModel Player => Run.Player;
        public MovementSystem Movement => scope.Movement;
        public NavigationService Navigation => scope.Navigation;
        public LowAiController Ai => scope.Ai;
        public DamageService Damage => scope.Damage;
        public DeathService Death => scope.Death;
        public ContactDamageSystem Contact => scope.Contact;
        public RunSimulation Simulation => scope.Simulation;
        public RunClock Clock => Run.Clock;
        public RunResult Result => coordinator.Result;
        public Task RestartAsync() => coordinator.RestartAsync();
        private RunTestRig(int seed, IChunkGenerator terrain, bool enableAi, bool enableCombat, ISceneLoader loader)
        {
            int nextSeed = seed;
            coordinator = new RunCoordinator(loader ?? new ImmediateSceneLoader(), () =>
                scope = new RigBuilder(unchecked(nextSeed++), terrain, enableAi, enableCombat));
            coordinator.StartRunAsync().GetAwaiter().GetResult();
            if (coordinator.Phase != RunPhase.Running) throw new InvalidOperationException(coordinator.LastError);
        }
        public static RunTestRig Create(int seed = 1234, bool scheduledSpawns = false, IChunkGenerator terrain = null,
            bool enableAi = true, bool enableCombat = true, ISceneLoader sceneLoader = null)
        {
            if (scheduledSpawns) throw new NotSupportedException("Scheduled spawning belongs to phase D1.");
            return new RunTestRig(seed, terrain, enableAi, enableCombat, sceneLoader);
        }
        private static UnitDefinition Definition(UnitKind kind, double health)
        {
            double speed = kind == UnitKind.Player ? 3 : kind == UnitKind.Air ? 6 : kind == UnitKind.Boss ? 1.1 : 1.5;
            return new UnitDefinition(kind.ToString(), kind, health, speed, kind == UnitKind.Boss ? 0.75 : kind == UnitKind.Player ? 0.28 : 0.26,
                kind == UnitKind.Player ? 0 : 5, kind == UnitKind.Player ? BigInteger.Zero : BigInteger.One);
        }
        public long Spawn(UnitKind kind, DVec2 position, double health = 10)
        {
            UnitFactory factory = kind == UnitKind.Player ? (UnitFactory)new PlayerFactory(World.Units) :
                kind == UnitKind.Air ? new AirEnemyFactory(World.Units) : new GroundEnemyFactory(World.Units);
            return factory.Spawn(new UnitSpawnRequest(World.Units.RunId, kind, WorldPosition.FromLocal(position), Definition(kind, health))).Id;
        }
        public void PlacePlayer(DVec2 position) => World.MoveUnit(Player.Id, WorldPosition.FromLocal(position));
        public long DropXp(DVec2 position, BigInteger value) => World.AddExperience(WorldPosition.FromLocal(position), value);
        public bool Hit(long targetId, double amount)
        {
            long attack = Run.AllocateAttackId();
            bool applied = Damage.TryApply(new DamageRequest(new HitKey(Run.Id, attack, 0, 0), Player.Id, targetId, amount, new DVec2(1, 0)), Clock.ElapsedSeconds);
            Damage.CompleteAttack(attack); return applied;
        }
        public UnitModel Unit(long id) => World.Units.Get(id);
        public void Advance(double seconds)
        {
            double steps = seconds / 0.02;
            if (seconds < 0 || double.IsNaN(seconds) || double.IsInfinity(seconds) || Math.Abs(steps - Math.Round(steps)) > 1e-8 || steps > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(seconds), "Use an integer multiple of the 0.02 second simulation step.");
            for (int i = 0; i < (int)Math.Round(steps) && Run.Phase == RunPhase.Running; i++)
            {
                if (Simulation != null) { Simulation.Step(0.02); continue; }
                if (Ai != null) { Ai.Tick(0.02); Movement.SetMoveIntent(Player.Id, Player.MoveIntent); }
                Movement.Step(0.02); Clock.Advance();
            }
        }
        public void Dispose() => coordinator.Dispose();

        private sealed class ImmediateSceneLoader : ISceneLoader
        {
            public Task LoadBattleAsync() => Task.CompletedTask;
            public Task LoadMainMenuAsync() => Task.CompletedTask;
        }

        private sealed class RigBuilder : IRunBuilder
        {
            private readonly bool enableAi, enableCombat;
            private bool disposed;
            public RunModel Run { get; }
            public MovementSystem Movement { get; private set; }
            public NavigationService Navigation { get; private set; }
            public LowAiController Ai { get; private set; }
            public DamageService Damage { get; private set; }
            public DeathService Death { get; private set; }
            public ContactDamageSystem Contact { get; private set; }
            public RunSimulation Simulation { get; private set; }
            public RigBuilder(int seed, IChunkGenerator terrain, bool enableAi, bool enableCombat)
            {
                this.enableAi = enableAi; this.enableCombat = enableCombat;
                var definitions = new DefinitionCatalog(Enum.GetValues(typeof(UnitKind)).Cast<UnitKind>()
                    .Select(kind => Definition(kind, kind == UnitKind.Player ? 100 : 10)));
                Run = new RunModel(Guid.NewGuid(), seed, definitions, terrain ?? new ChunkGenerator(seed, MapSettings.TestDefaults(0)));
            }
            public void BuildWorld() => Run.World.GetChunk(default);
            public void CreatePlayer()
            {
                Run.SetPlayer((PlayerModel)new PlayerFactory(Run.World.Units).Spawn(
                    new UnitSpawnRequest(Run.Id, UnitKind.Player, default, Run.Definitions.GetUnit(UnitKind.Player))));
                Navigation = new NavigationService(Run.World); Movement = new MovementSystem(Run.World, Navigation, Run.Player);
                if (enableAi) Ai = new LowAiController(Run.Player, Run.World, Navigation);
                Damage = new DamageService(Run, Movement); Death = new DeathService(Run, Damage);
                Contact = new ContactDamageSystem(Run, Movement, Damage);
                if (enableCombat) Simulation = new RunSimulation(Run, Movement, Ai, Damage, Death, Contact);
            }
            public void CreateInitialEnemies() { }
            public void Dispose()
            {
                if (disposed) return; disposed = true;
                Simulation?.Dispose(); Death?.Dispose(); Damage?.Dispose(); Movement?.Dispose(); Run.Dispose();
            }
        }
    }
}
