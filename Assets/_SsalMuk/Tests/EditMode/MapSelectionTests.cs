using System;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests.MapFeature
{
    public sealed class MapSelectionTests
    {
        [Test]
        public void LibraryInitialEnemiesAlsoStartOutsideCameraInsideTheWalls()
        {
            var catalog = UnityEngine.Resources.Load<SsalMuk.Unity.GameCatalog>("Bootstrap/GameCatalog");
            using var run = new RunModel(Guid.NewGuid(), 73, catalog.CreateDefinitions(), Generator("Library"));
            var builder = new SsalMuk.Unity.InitialRunBuilder(run, catalog.Defaults, null);
            builder.BuildWorld(); builder.CreatePlayer(); builder.CreateInitialEnemies();
            double halfWidth = UnityEngine.Camera.main != null ? UnityEngine.Camera.main.orthographicSize * UnityEngine.Camera.main.aspect : catalog.Defaults.CameraOrthographicSize * (16.0 / 9);
            int count = 0;
            foreach (var unit in run.Units)
            {
                if (unit.Kind == UnitKind.Player) continue;
                count++;
                Assert.That(Math.Abs(run.Player.Position.DisplacementTo(unit.Position).X), Is.GreaterThan(halfWidth + unit.BodyRadius));
                Assert.That(run.World.Query.IsCircleFree(unit.Position, unit.BodyRadius), Is.True);
            }
            Assert.That(count, Is.EqualTo(catalog.Defaults.InitialEnemyCount));
        }
        private static IChunkGenerator Generator(string map, int seed = 73)
            => new ThemedChunkGenerator(seed, new MapSettings(.7, maximumBodyRadius: 1.125), (MapKind)Enum.Parse(typeof(MapKind), map));
        [Test]
        public void ForestGrassNeverBlocksAndAllClearingCellsRemainOpenAcrossSeeds()
        {
            int trees = 0, rocks = 0;
            for (int seed = 0; seed < 24; seed++)
            {
                var chunk = Generator("Forest", seed).Generate(new ChunkCoord(-seed, seed));
                var occupied = new bool[1024]; int grass = 0;
                Assert.That(chunk.Clearings.Count, Is.GreaterThan(1));
                foreach (var room in chunk.Clearings)
                    for (int y = room.Y; y < room.Y + room.Height; y++) for (int x = room.X; x < room.X + room.Width; x++)
                        Assert.That(chunk.IsBlocked(x, y), Is.False);
                foreach (var prop in chunk.Scenery)
                {
                    if (!prop.BlocksMovement) { grass++; Assert.That(chunk.IsBlocked(prop.X, prop.Y), Is.False); continue; }
                    if (prop.Kind == ForestPropKind.Tree) trees++; else rocks++;
                    for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++) occupied[(prop.Y + dy) * 32 + prop.X + dx] = true;
                }
                Assert.That(grass, Is.GreaterThan(100));
                for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) Assert.That(chunk.IsBlocked(x, y), Is.EqualTo(occupied[y * 32 + x]));
            }
            Assert.That(trees, Is.GreaterThan(0)); Assert.That(rocks, Is.GreaterThan(0));
        }
        [Test]
        public void LibraryWaveAndBossSpawnInsideWallsAndOutsideTheCamera()
        {
            using var rig = RunTestRig.Create(terrain: Generator("Library"), enableAi: false);
            rig.PlacePlayer(new DVec2(16.5, 16.5));
            using var spawns = new SpawnDirector(rig.Run);
            var view = new WorldRect(rig.Player.Position, 16, 9);
            for (int i = 0; i < 12; i++) spawns.Tick(300, view);
            Assert.That(spawns.SpawnedCount(UnitKind.Boss), Is.EqualTo(1));
            Assert.That(spawns.SurroundWaveSpawnedCount, Is.GreaterThan(0));
            bool left = false, right = false;
            foreach (var unit in rig.Run.Units)
            {
                if (unit.Kind != UnitKind.Normal && unit.Kind != UnitKind.Boss) continue;
                Assert.That(unit.Position.Chunk.Y, Is.Zero);
                Assert.That(rig.World.Query.IsCircleFree(unit.Position, unit.BodyRadius), Is.True);
                double dx = rig.Player.Position.DisplacementTo(unit.Position).X;
                Assert.That(Math.Abs(dx), Is.GreaterThan(16)); left |= dx < 0; right |= dx > 0;
            }
            Assert.That(left && right, Is.True);
        }
        [Test]
        public void ForestRecreatesNegativeChunksAndKeepsBossTravelLanesOpen()
        {
            var generator = Generator("Forest");
            var coord = new ChunkCoord(-72, 53); var first = generator.Generate(coord);
            generator.Generate(new ChunkCoord(91, -83));
            Assert.That(generator.Generate(coord).Fingerprint, Is.EqualTo(first.Fingerprint));
            Assert.That(Generator("Forest", 74).Generate(coord).Fingerprint, Is.Not.EqualTo(first.Fingerprint));
            int blocked = 0;
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                if (first.IsBlocked(x, y)) blocked++;
                if (x >= 13 && x <= 18 || y >= 13 && y <= 18)
                    Assert.That(first.IsBlocked(x, y), Is.False, "Connected central travel lanes must remain clear.");
            }
            Assert.That(blocked, Is.GreaterThan(0));
            using var world = new WorldStore(new UnitRegistry(Guid.NewGuid()), generator);
            var start = new WorldPosition(coord, new DVec2(16.5, 16.5));
            Assert.That(world.Query.SweepCircle(start, new DVec2(64, 0), 1.125), Is.Null);
            Assert.That(world.Query.SweepCircle(start, new DVec2(0, -64), 1.125), Is.Null);
        }
        [Test]
        public void LibraryHasContinuousHorizontalInteriorAndSolidUpperLowerWalls()
        {
            var generator = Generator("Library");
            using var world = new WorldStore(new UnitRegistry(Guid.NewGuid()), generator);
            var start = new WorldPosition(new ChunkCoord(-7, 0), new DVec2(16.5, 16.5));
            Assert.That(world.Query.SweepCircle(start, new DVec2(100, 0), 1.125), Is.Null);
            Assert.That(world.Query.SweepCircle(start, new DVec2(0, 20), .28).HasValue, Is.True);
            Assert.That(world.Query.SweepCircle(start, new DVec2(0, -20), .28).HasValue, Is.True);
            Assert.That(world.Query.IsCircleFree(start.Offset(new DVec2(0, 2)), .28), Is.True, "Vertical movement inside the strip must work.");
            foreach (long x in new long[] { -100, -1, 0, 100 })
            {
                var chunk = generator.Generate(new ChunkCoord(x, 0));
                for (int col = 0; col < 32; col++)
                { Assert.That(chunk.IsBlocked(col, 2), Is.True); Assert.That(chunk.IsBlocked(col, 29), Is.True); }
                Assert.That(generator.Generate(new ChunkCoord(x, 1)).IsBlocked(16, 16), Is.True);
                Assert.That(generator.Generate(new ChunkCoord(x, -1)).IsBlocked(16, 16), Is.True);
            }
        }
    }
}
