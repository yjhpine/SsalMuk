using System;
using SsalMuk.Core;

namespace SsalMuk.Unity
{
    public sealed class InitialRunBuilder : IRunBuilder
    {
        private readonly DevelopmentDefaults defaults;
        private readonly Action stageReady;
        private readonly IRandomSource spawnRandom;
        private bool worldBuilt, playerCreated, enemiesCreated;
        public RunModel Run { get; }
        public InitialRunBuilder(RunModel run, DevelopmentDefaults defaults, Action stageReady)
        { Run = run; this.defaults = defaults; this.stageReady = stageReady; spawnRandom = new SeedStreams(run.Seed).Spawn; }
        public void BuildWorld()
        {
            if (worldBuilt) throw new InvalidOperationException("World has already been built.");
            PrepareTerrain(); worldBuilt = true; stageReady?.Invoke();
        }
        public void PrepareTerrain()
        {
            var center = Run.ViewOrigin.Chunk;
            for (long y = -1; y <= 1; y++) for (long x = -1; x <= 1; x++)
                Run.World.GetChunk(new ChunkCoord(checked(center.X + x), checked(center.Y + y)));
        }
        public void CreatePlayer()
        {
            if (!worldBuilt || playerCreated) throw new InvalidOperationException("Player creation must follow world creation exactly once.");
            var position = new WorldPosition(default, new DVec2(16.5, 16.5)); var definition = Run.Definitions.GetUnit(UnitKind.Player);
            if (!Run.World.Query.IsCircleFree(position, definition.BodyRadius)) throw new InvalidOperationException("Initial player position is blocked.");
            var player = (PlayerModel)new PlayerFactory(Run.World.Units).Spawn(new UnitSpawnRequest(Run.Id, UnitKind.Player, position, definition));
            Run.SetPlayer(player); playerCreated = true; stageReady?.Invoke();
        }
        public void CreateInitialEnemies()
        {
            if (!playerCreated || enemiesCreated) throw new InvalidOperationException("Enemies must be created after the player exactly once.");
            var definition = Run.Definitions.GetUnit(UnitKind.Normal); var factory = new GroundEnemyFactory(Run.World.Units);
            for (int i = 0; i < defaults.InitialEnemyCount; i++)
            {
                bool spawned = false;
                for (int attempt = 0; attempt < 256; attempt++)
                {
                    double angle = spawnRandom.NextUnit() * Math.PI * 2, distance = 7 + spawnRandom.NextUnit() * 5;
                    var position = Run.Player.Position.Offset(new DVec2(Math.Cos(angle), Math.Sin(angle)) * distance);
                    if (!Run.World.Query.IsCircleFree(position, definition.BodyRadius)) continue;
                    bool occupied = false;
                    foreach (var unit in Run.Units)
                        if (position.DistanceTo(unit.Position) < unit.BodyRadius + definition.BodyRadius + 0.1) { occupied = true; break; }
                    if (occupied) continue;
                    factory.Spawn(new UnitSpawnRequest(Run.Id, UnitKind.Normal, position, definition)); spawned = true; break;
                }
                if (!spawned) throw new InvalidOperationException("Could not place all initial enemies safely.");
            }
            enemiesCreated = true; stageReady?.Invoke();
        }
        public void Dispose() => Run.Dispose();
    }
}
