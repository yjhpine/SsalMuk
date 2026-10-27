using System;
using System.Linq;
using NUnit.Framework;
using SsalMuk.Core;
using SsalMuk.Unity;

namespace SsalMuk.Tests.LibraryExploration
{
    public sealed class LibraryExplorationTests
    {
        private static IChunkGenerator Map(double density = .2) => new ThemedChunkGenerator(73,
            new MapSettings(density, maximumBodyRadius: 1.125), MapKind.Library);

        [Test]
        public void InitialTerrainContainsOnlyTheThreeHorizontalChunks()
        {
            var catalog = UnityEngine.Resources.Load<GameCatalog>("Bootstrap/GameCatalog");
            using var run = new RunModel(Guid.NewGuid(), 73, catalog.CreateDefinitions(), Map());
            var builder = new InitialRunBuilder(run, catalog.Defaults, null);
            builder.BuildWorld();
            Assert.That(run.World.CachedChunkCount, Is.EqualTo(3));
            Assert.That(run.Terrain.All(x => x.Coord.Y == 0), Is.True);
        }

        [Test]
        public void SmallAndLongOutsideQueriesBlockWithoutCreatingVerticalChunks()
        {
            using var world = new WorldStore(new UnitRegistry(Guid.NewGuid()), Map());
            foreach (long row in new long[] { -100, -1, 1, 100 })
            {
                var outside = new WorldPosition(new ChunkCoord(-2, row), new DVec2(16, 16));
                Assert.That(world.Query.IsCircleFree(outside, .28), Is.False);
                Assert.Throws<InvalidOperationException>(() => world.Query.SweepCircle(outside, new DVec2(1, 0), .28));
                Assert.That(world.IsBlocked(GridCell.At(outside)), Is.True);
            }
            var inside = WorldPosition.FromLocal(new DVec2(16, 16));
            Assert.That(world.Query.SweepCircle(inside, new DVec2(0, 100), .28), Is.Not.Null);
            Assert.That(world.Query.SweepCircle(inside, new DVec2(0, -100), .28), Is.Not.Null);
            Assert.That(world.CachedTerrain.All(x => x.Coord.Y == 0), Is.True);
        }

        [Test]
        public void DenserFurniturePreservesBossLaneAndStableRecreation()
        {
            int count = 0;
            for (int seed = 0; seed < 20; seed++)
            {
                var gen = new ThemedChunkGenerator(seed, new MapSettings(.2, maximumBodyRadius: 1.125), MapKind.Library);
                var coord = new ChunkCoord(-seed, 0); var chunk = gen.Generate(coord);
                count += chunk.Furniture.Count;
                Assert.That(gen.Generate(coord).Fingerprint, Is.EqualTo(chunk.Fingerprint));
                using var world = new WorldStore(new UnitRegistry(Guid.NewGuid()), gen);
                Assert.That(world.Query.SweepCircle(new WorldPosition(coord, new DVec2(0, 16)), new DVec2(64, 0), 1.125), Is.Null);
            }
            Assert.That(count, Is.GreaterThanOrEqualTo(140), "At least seven furnishings on average, with reserved travel lanes.");
        }

        [TestCase(3.8, 1)]
        [TestCase(28.2, -1)]
        public void IdlePlayerMovesAwayFromLibraryWall(double y, int inward)
        {
            using var rig = RunTestRig.Create(terrain: Map(0));
            rig.PlacePlayer(new DVec2(16, y)); rig.Ai.Tick(.02);
            Assert.That(rig.Player.MoveIntent.Y * inward, Is.GreaterThan(.1));
        }

        [Test]
        public void EscapeDirectionPrefersInteriorWhenThreatCostsAreEqual()
        {
            using var rig = RunTestRig.Create(terrain: Map(0));
            rig.PlacePlayer(new DVec2(16, 4));
            var context = new AiContext(rig.Player, rig.World, rig.Navigation, new AiSettings());
            new EvadeState().Tick(context, .02);
            Assert.That(context.MoveIntent.Y, Is.GreaterThan(.3));
        }

        [Test]
        public void WallPreferenceStillCollectsExperienceAndChecksContactBeforeMovingInward()
        {
            using var rig = RunTestRig.Create(terrain: Map(0));
            rig.PlacePlayer(new DVec2(16, 6));
            long xp = rig.DropXp(new DVec2(16, 3.4), 20);
            rig.Advance(4);
            Assert.That(rig.World.TryGetExperience(xp, out _), Is.False, "Wall comfort must not make near-wall loot unreachable.");
            rig.PlacePlayer(new DVec2(16, 4));
            rig.Spawn(UnitKind.Normal, new DVec2(16, 5.1));
            rig.Ai.Tick(.02);
            Assert.That(rig.Player.BrainState, Is.Not.EqualTo(BrainState.Collect), "Predict contact from the wall-adjusted intent.");
        }

        [Test]
        public void NavigationCrossesHorizontalChunksButDoesNotSearchVerticalOnes()
        {
            using var world = new WorldStore(new UnitRegistry(Guid.NewGuid()), Map());
            var nav = new NavigationService(world);
            var start = WorldPosition.FromLocal(new DVec2(16, 16));
            var path = nav.RequestPath(start, new WorldPosition(new ChunkCoord(-3, 0), new DVec2(16, 16)), 1.125);
            for (int i = 0; i < 80 && path.Status == PathStatus.Pending; i++) nav.Advance(256);
            Assert.That(path.Status, Is.EqualTo(PathStatus.Ready));
            var invalid = nav.RequestPath(start, new WorldPosition(new ChunkCoord(0, 1), new DVec2(16, 16)), .28);
            for (int i = 0; i < 80 && invalid.Status == PathStatus.Pending; i++) nav.Advance(256);
            Assert.That(invalid.Status, Is.EqualTo(PathStatus.NoPath));
            Assert.That(world.CachedTerrain.All(x => x.Coord.Y == 0), Is.True);
        }

        [Test]
        public void WallComfortDoesNotPreventCollectingNearWallPowerups()
        {
            using var rig = RunTestRig.Create(terrain: Map(0));
            rig.PlacePlayer(new DVec2(16, 7));
            long item = rig.World.AddItem(PowerupKind.MoveSpeed, WorldPosition.FromLocal(new DVec2(16, 3.4)));
            rig.Advance(5);
            Assert.That(rig.World.TryGetItem(item, out _), Is.False);
            Assert.That(rig.Player.MoveSpeed, Is.GreaterThan(3));
        }

        [Test]
        public void EscapeSafetyOutranksWallComfort()
        {
            var catalog = UnityEngine.Resources.Load<GameCatalog>("Bootstrap/GameCatalog");
            using var rig = RunTestRig.Create(terrain: Map(0), definitions: catalog.CreateDefinitions(), enableCombat: false);
            var origin = new DVec2(16, 4.3597963482); rig.PlacePlayer(origin);
            foreach (var relative in new[] { new DVec2(-1.1374599836, -.2735918563), new DVec2(-.8629979008, 2.1007279264),
                new DVec2(-1.9269261284, 2.4293855123), new DVec2(.8618035672, -.1062931924) }) rig.Spawn(UnitKind.Normal, origin + relative);
            rig.Ai.Tick(.02);
            Assert.That(rig.Player.BrainState, Is.EqualTo(BrainState.Evade));
            foreach (var enemy in rig.Run.Units.Where(x => x.Kind != UnitKind.Player))
            {
                var relative = rig.Player.Position.DisplacementTo(enemy.Position);
                var travel = (-relative.Normalized * enemy.MoveSpeed - rig.Player.MoveIntent * rig.Player.MoveSpeed) * .35;
                Assert.That(CircleContact.Interval(relative, travel, enemy.BodyRadius + rig.Player.BodyRadius, out _, out _), Is.False);
            }
        }

        [Test]
        public void BreakoutPrefersAnEmptyCorridorBeforeWallComfort()
        {
            var catalog = UnityEngine.Resources.Load<GameCatalog>("Bootstrap/GameCatalog");
            using var rig = RunTestRig.Create(terrain: Map(0), definitions: catalog.CreateDefinitions(), enableCombat: false);
            var origin = new DVec2(16, 4.2018807473); rig.PlacePlayer(origin);
            foreach (var relative in new[] { new DVec2(-2.0692826933, 2.0788550425), new DVec2(-.6606839173, -.6805810591),
                new DVec2(2.5939704263, .9730504890), new DVec2(-.9163766729, .6839320508),
                new DVec2(.4800242522, .8941579765), new DVec2(1.1550579987, 1.2083397743) }) rig.Spawn(UnitKind.Normal, origin + relative);
            rig.Ai.Tick(.02);
            Assert.That(rig.Player.BrainState, Is.EqualTo(BrainState.Breakout));
            var context = new AiContext(rig.Player, rig.World, rig.Navigation, new AiSettings());
            foreach (var enemy in rig.Run.Units.Where(x => x.Kind != UnitKind.Player))
                Assert.That(context.InCorridor(enemy, rig.Player.MoveIntent, context.Settings.ProbeDistance), Is.False);
        }
    }
}
