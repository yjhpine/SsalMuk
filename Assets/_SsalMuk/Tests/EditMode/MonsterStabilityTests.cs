using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SsalMuk.Core;
using SsalMuk.Presentation;
using SsalMuk.Unity;
using UnityEditor;
using UnityEngine;

namespace SsalMuk.Tests
{
    public sealed class MonsterStabilityTests
    {
        [Test]
        public void FullMapNormalLimitPreservesExistingUnitsAndSkipsSpawnDebt()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            var ids = Enumerable.Range(1, 3).Select(i => rig.Spawn(UnitKind.Normal, new DVec2(i * 100, 0))).ToArray();
            using var director = new SpawnDirector(rig.Run, new SpawnSettings(normalBaseRate: 10, normalRateGrowth: 0, airInterval: 10000, normalPopulationCap: 3));
            var bounds = new WorldRect(rig.Player.Position, 16, 9);
            director.Tick(100, bounds);
            Assert.That(rig.Run.Units.Count(x => x.Kind == UnitKind.Normal), Is.EqualTo(3));
            Assert.That(ids.All(id => rig.World.Units.TryGet(id, out var unit) && unit.IsAlive), Is.True);
            Assert.That(director.Pending.Any(x => x.Kind == UnitKind.Normal), Is.False);
            rig.World.Units.Remove(ids[0]);
            director.Tick(100, bounds);
            Assert.That(rig.Run.Units.Count(x => x.Kind == UnitKind.Normal), Is.EqualTo(2), "Skipped deadlines must not be replayed when a slot opens.");
            director.Tick(100.1, bounds);
            Assert.That(rig.Run.Units.Count(x => x.Kind == UnitKind.Normal), Is.EqualTo(3));
            Assert.That(director.SpawnedCount(UnitKind.Normal), Is.EqualTo(1));
        }

        [Test]
        public void BlockedNormalSpawnsReserveOnlyAvailableSlots()
        {
            var terrain = new BlockedSpawnTerrain();
            using var rig = RunTestRig.Create(enableAi: false, terrain: terrain);
            using var director = new SpawnDirector(rig.Run, new SpawnSettings(normalBaseRate: 10, normalRateGrowth: 0, airInterval: 10000, normalPopulationCap: 3));
            var bounds = new WorldRect(rig.Player.Position, 16, 9);
            director.Tick(100, bounds); director.Tick(200, bounds);
            Assert.That(director.Pending.Where(x => x.Kind == UnitKind.Normal).Sum(x => x.Remaining), Is.EqualTo(3));
            terrain.Blocked = false; rig.World.ClearTerrainCache(); director.Tick(200, bounds);
            Assert.That(director.SpawnedCount(UnitKind.Normal), Is.EqualTo(3));
            Assert.That(director.Pending, Is.Empty);
        }

        [Test]
        public void FullNormalPopulationDoesNotBlockAirWavesOrOverlappingBosses()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            rig.Spawn(UnitKind.Normal, new DVec2(100, 0));
            using var director = new SpawnDirector(rig.Run, new SpawnSettings(normalRateGrowth: 0, airBaseCount: 2, airCountGrowthSeconds: 100000, normalPopulationCap: 1));
            var bounds = new WorldRect(rig.Player.Position, 16, 9);
            director.Tick(300, bounds); director.Tick(600, bounds);
            Assert.That(rig.Run.Units.Count(x => x.Kind == UnitKind.Normal), Is.EqualTo(1));
            Assert.That(director.SpawnedCount(UnitKind.Air), Is.EqualTo(60));
            Assert.That(director.SpawnedCount(UnitKind.Boss), Is.EqualTo(2));
            Assert.That(director.Pending, Is.Empty);
        }

        [Test]
        public void InitialEnemiesRespectTheEditablePopulationLimit()
        {
            var defaults = ScriptableObject.CreateInstance<DevelopmentDefaults>();
            try
            {
                var serialized = new SerializedObject(defaults);
                var cap = serialized.FindProperty("normalPopulationCap");
                Assert.That(cap, Is.Not.Null, "The population limit must be editable with the other development settings.");
                cap.intValue = 3; serialized.FindProperty("initialEnemyCount").intValue = 12;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                using var run = new RunModel(Guid.NewGuid(), 1234, defaults.CreateCatalog(), new ChunkGenerator(1234, MapSettings.TestDefaults(0)));
                var builder = new InitialRunBuilder(run, defaults, null);
                builder.BuildWorld(); builder.CreatePlayer(); builder.CreateInitialEnemies();
                Assert.That(run.Units.Count(x => x.Kind == UnitKind.Normal), Is.EqualTo(3));
            }
            finally { UnityEngine.Object.DestroyImmediate(defaults); }
        }

        [Test]
        public void UnitAndCameraOriginInterpolateBetweenSimulationTicks()
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            long enemy = rig.Spawn(UnitKind.Normal, new DVec2(4, 0));
            rig.Movement.SetMoveIntent(rig.Player.Id, new DVec2(1, 0));
            rig.Movement.SetMoveIntent(enemy, new DVec2(-1, 0));
            rig.Movement.Step(0.02);
            var view = new PositionView(); var presenter = new WorldPresenter(view);
            presenter.Refresh(rig.Run, 0); Assert.That(view.Positions[enemy].X, Is.EqualTo(4).Within(1e-10));
            presenter.Refresh(rig.Run, 0.5); Assert.That(view.Positions[enemy].X, Is.EqualTo(3.955).Within(1e-10));
            Assert.That(view.Positions[rig.Player.Id], Is.EqualTo(DVec2.Zero));
            presenter.Refresh(rig.Run, 1); Assert.That(view.Positions[enemy].X, Is.EqualTo(3.91).Within(1e-10));
            Assert.That(rig.Unit(enemy).Position.Local.X, Is.EqualTo(3.97).Within(1e-10), "Rendering must not move the combat model.");
        }

        [Test]
        public void NewlySpawnedUnitsDoNotInterpolateFromWorldZero()
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            var origin = new WorldPosition(new ChunkCoord(9007199254740993, -7), new DVec2(16, 16));
            rig.World.MoveUnit(rig.Player.Id, origin); rig.Movement.Step(0.02);
            var enemy = WorldStoreTests.Spawn(rig.World, UnitKind.Normal, origin.Offset(new DVec2(4, 0)));
            var view = new PositionView(); new WorldPresenter(view).Refresh(rig.Run, 0);
            Assert.That(view.Positions[enemy.Id], Is.EqualTo(new DVec2(4, 0)));
        }

        [Test]
        public void ASharedRouteKeepsMovingWhileThePlayerChangesGridCells()
        {
            using var world = NavigationTests.Layout((x, y) => x == 8 && y >= 4 && y <= 18);
            var navigation = new NavigationService(world);
            var enemy = WorldStoreTests.Spawn(world, UnitKind.Normal, NavigationTests.P(4.5, 12.5));
            var target = NavigationTests.P(14.5, 12.5);
            var field = navigation.GetFlowField(target, enemy.BodyRadius);
            for (int i = 0; i < 100 && !field.IsComplete; i++) navigation.Advance(256);
            Assert.That(field.IsComplete, Is.True);
            var before = navigation.ChaseDirection(enemy, target); Assert.That(before.Length, Is.GreaterThan(0.9));
            for (int i = 0; i < 20; i++)
            {
                target = NavigationTests.P(i % 2 == 0 ? 15.5 : 14.5, 12.5);
                var direction = navigation.ChaseDirection(enemy, target);
                Assert.That(direction.Length, Is.GreaterThan(0.9), "A valid old shared route must survive a pending replacement.");
                Assert.That(world.Query.SweepCircle(enemy.Position, direction * 0.03, enemy.BodyRadius), Is.Null);
                navigation.Advance(1);
            }
        }

        [Test]
        public void AChangingTargetDoesNotStarveAnUnfinishedFarRoute()
        {
            using var world = NavigationTests.Layout((x, y) => false);
            var navigation = new NavigationService(world);
            var enemy = WorldStoreTests.Spawn(world, UnitKind.Normal, NavigationTests.P(100.5, 12.5));
            bool moved = false;
            for (int i = 0; i < 300 && !moved; i++)
            {
                moved = navigation.ChaseDirection(enemy, NavigationTests.P(i % 2 == 0 ? 1.5 : 2.5, 12.5)).Length > 0.9;
                navigation.Advance(16);
            }
            Assert.That(moved, Is.True, "Repeated target changes must not cancel every in-progress route before it can finish.");
        }

        private sealed class PositionView : IWorldView
        {
            public readonly Dictionary<long, DVec2> Positions = new Dictionary<long, DVec2>();
            public void BeginFrame(Guid runId) => Positions.Clear();
            public void ShowTerrain(ChunkData chunk, DVec2 origin) { }
            public void ShowUnit(UnitModel unit, DVec2 position) => Positions[unit.Id] = position;
            public void EndFrame(double seconds, int enemyCount) { }
        }
        private sealed class BlockedSpawnTerrain : IChunkGenerator
        {
            public bool Blocked = true;
            public ChunkData Generate(ChunkCoord coord)
            {
                var cells = new bool[1024];
                if (Blocked) for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                    cells[y * 32 + x] = new WorldPosition(coord, new DVec2(x + 0.5, y + 0.5)).DistanceTo(default) > 2;
                return new ChunkData(coord, cells, 1);
            }
        }
    }
}
