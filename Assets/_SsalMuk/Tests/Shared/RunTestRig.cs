using System;
using System.Numerics;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class RunTestRig : IDisposable
    {
        public WorldStore World { get; }
        public PlayerModel Player { get; }
        public MovementSystem Movement { get; }
        public NavigationService Navigation { get; }
        public RunClock Clock { get; }
        private RunTestRig(int seed)
        {
            World = new WorldStore(new UnitRegistry(Guid.NewGuid()), new ChunkGenerator(seed, MapSettings.TestDefaults(0)));
            Player = (PlayerModel)new PlayerFactory(World.Units).Spawn(new UnitSpawnRequest(World.Units.RunId, UnitKind.Player, default, Definition(UnitKind.Player, 100)));
            Navigation = new NavigationService(World); Movement = new MovementSystem(World, Navigation, Player);
            Clock = new RunClock(0.02); Clock.Start();
        }
        public static RunTestRig Create(int seed = 1234, bool scheduledSpawns = false)
        {
            if (scheduledSpawns) throw new NotSupportedException("Scheduled spawning belongs to phase D1.");
            return new RunTestRig(seed);
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
        public UnitModel Unit(long id) => World.Units.Get(id);
        public void Advance(double seconds)
        {
            double steps = seconds / 0.02;
            if (seconds < 0 || double.IsNaN(seconds) || double.IsInfinity(seconds) || Math.Abs(steps - Math.Round(steps)) > 1e-8 || steps > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(seconds), "Use an integer multiple of the 0.02 second simulation step.");
            for (int i = 0; i < (int)Math.Round(steps); i++) { Movement.Step(0.02); Clock.Advance(); }
        }
        public void Dispose() { Movement.Dispose(); World.Dispose(); }
    }
}
