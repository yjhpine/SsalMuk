using System;
using System.Numerics;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class ThemedNavigationContractTests
    {
        [TestCase(MapKind.Forest)]
        [TestCase(MapKind.Library)]
        public void PendingCrossChunkDetourRemainsSafeAfterTerrainEviction(MapKind map)
        {
            using var world = CreateWorld(map);
            var coord = new ChunkCoord(-2, map == MapKind.Library ? 0 : 3);
            FindBlockedCrossChunkTrip(world, coord, out var start, out var target);
            const double radius = .28;
            Assert.That(start.Chunk, Is.Not.EqualTo(target.Chunk), "The route must cross a chunk boundary.");
            Assert.That(start.DistanceTo(target), Is.GreaterThan(12));
            Assert.That(world.Query.SweepCircle(start, start.DisplacementTo(target), radius), Is.Not.Null,
                "A clear central aisle would not exercise obstacle avoidance.");

            var navigation = new NavigationService(world);
            var request = navigation.RequestPath(start, target, radius);
            navigation.Advance(4);
            Assert.That(request.Status, Is.EqualTo(PathStatus.Pending), "Evict during the search, not after completion.");
            Assert.That(world.EvictTerrain(start.Chunk), Is.True);
            Assert.That(world.EvictTerrain(target.Chunk), Is.True);
            NavigationTests.Complete(navigation, request);

            Assert.That(request.Status, Is.EqualTo(PathStatus.Ready));
            Assert.That(request.Waypoints, Is.Not.Empty);
            Assert.That(request.Waypoints[request.Waypoints.Count - 1], Is.EqualTo(target));
            NavigationTests.AssertSafe(world, start, request.Waypoints, radius);
            if (map == MapKind.Library)
            {
                foreach (var waypoint in request.Waypoints) Assert.That(waypoint.Chunk.Y, Is.Zero);
                foreach (var chunk in world.CachedTerrain) Assert.That(chunk.Coord.Y, Is.Zero);
            }
        }

        [TestCase(MapKind.Forest)]
        [TestCase(MapKind.Library)]
        public void ReverseRegenerationPreservesThemedTerrainAndRetainedState(MapKind map)
        {
            using var world = CreateWorld(map);
            var firstCoord = new ChunkCoord(-40, map == MapKind.Library ? 0 : 19);
            var secondCoord = new ChunkCoord(7, map == MapKind.Library ? 0 : -11);
            var first = world.GetChunk(firstCoord);
            var second = world.GetChunk(secondCoord);
            var normalPosition = new WorldPosition(firstCoord, new DVec2(16, 16));
            var bossPosition = new WorldPosition(secondCoord, new DVec2(16, 16));
            var normal = WorldStoreTests.Spawn(world, UnitKind.Normal, normalPosition);
            var boss = WorldStoreTests.Spawn(world, UnitKind.Boss, bossPosition);
            double normalHealth = normal.Health, bossHealth = boss.Health;
            var groundedPosition = normalPosition.Offset(new DVec2(0, 2));
            long groundedId = world.AddExperience(groundedPosition, 7, 2);
            var flightStart = new WorldPosition(firstCoord, new DVec2(31.8, 16));
            var flightPosition = flightStart.Offset(new DVec2(.4, 0));
            Assert.That(flightStart.Chunk, Is.Not.EqualTo(flightPosition.Chunk));
            var flightValue = BigInteger.Pow(10, 30);
            long flightId = world.AddExperience(flightStart, flightValue, 3);
            Assert.That(world.TryBeginAttraction(world.Units.RunId, flightId, 5), Is.True);
            Assert.That(world.MoveExperience(world.Units.RunId, flightId, flightPosition), Is.True);

            Assert.That(world.EvictTerrain(firstCoord), Is.True);
            Assert.That(world.GetChunk(secondCoord), Is.SameAs(second), "Selective eviction must keep the other cached chunk.");
            world.ClearTerrainCache();
            Assert.That(world.CachedChunkCount, Is.Zero);
            AssertSameTerrain(second, world.GetChunk(secondCoord));
            AssertSameTerrain(first, world.GetChunk(firstCoord));

            Assert.That(world.Units.Count, Is.EqualTo(2));
            Assert.That(world.Units.Get(normal.Id), Is.SameAs(normal));
            Assert.That(world.Units.Get(boss.Id), Is.SameAs(boss));
            Assert.That(normal.Position, Is.EqualTo(normalPosition));
            Assert.That(boss.Position, Is.EqualTo(bossPosition));
            Assert.That(normal.Health, Is.EqualTo(normalHealth));
            Assert.That(boss.Health, Is.EqualTo(bossHealth));
            CollectionAssert.AreEquivalent(new[] { normal.Id }, world.Query.QueryCircle(normalPosition, .01));
            CollectionAssert.AreEquivalent(new[] { boss.Id }, world.Query.QueryCircle(bossPosition, .01));
            Assert.That(world.Experience.Count, Is.EqualTo(2));
            Assert.That(world.TryGetExperience(groundedId, out var grounded), Is.True);
            Assert.That(grounded.State, Is.EqualTo(ExperienceState.Grounded));
            Assert.That(grounded.Position, Is.EqualTo(groundedPosition));
            Assert.That(grounded.Value, Is.EqualTo(new BigInteger(7)));
            Assert.That(grounded.CreatedAt, Is.EqualTo(2));
            Assert.That(world.TryGetExperience(flightId, out var flight), Is.True);
            Assert.That(flight.State, Is.EqualTo(ExperienceState.Attracting));
            Assert.That(flight.PreviousPosition, Is.EqualTo(flightStart));
            Assert.That(flight.Position, Is.EqualTo(flightPosition));
            Assert.That(flight.Value, Is.EqualTo(flightValue));
            Assert.That(flight.CreatedAt, Is.EqualTo(3));
            Assert.That(flight.AttractionStartedAt, Is.EqualTo(5));
            CollectionAssert.AreEquivalent(new[] { flightId }, world.AttractingExperienceIds);
            CollectionAssert.AreEquivalent(new[] { groundedId }, world.Query.QueryExperienceCircle(groundedPosition, .01));
            CollectionAssert.AreEquivalent(new[] { flightId }, world.Query.QueryExperienceCircle(flightPosition, .01));
            Assert.That(world.Query.QueryExperienceCircle(flightStart, .01), Is.Empty);
        }

        private static WorldStore CreateWorld(MapKind map) => new WorldStore(new UnitRegistry(Guid.NewGuid()),
            new ThemedChunkGenerator(73, new MapSettings(.7, maximumBodyRadius: 1.125), map));

        private static void FindBlockedCrossChunkTrip(WorldStore world, ChunkCoord coord,
            out WorldPosition start, out WorldPosition target)
        {
            var chunk = world.GetChunk(coord);
            for (int y = 4; y <= 27; y++) for (int x = 3; x < 20; x++)
            {
                if (!chunk.IsBlocked(x, y)) continue;
                var candidate = new WorldPosition(coord, new DVec2(x - 1.5, y + .5));
                var destination = new WorldPosition(new ChunkCoord(coord.X + 1, coord.Y), new DVec2(1.5, y + .5));
                if (!world.Query.IsCircleFree(candidate, .28) || !world.Query.IsCircleFree(destination, .28)) continue;
                start = candidate; target = destination; return;
            }
            Assert.Fail("The seeded map must contain an accessible obstacle before the adjacent chunk.");
            start = target = default;
        }

        private static void AssertSameTerrain(ChunkData expected, ChunkData actual)
        {
            Assert.That(actual, Is.Not.SameAs(expected));
            Assert.That(actual.Coord, Is.EqualTo(expected.Coord));
            Assert.That(actual.Map, Is.EqualTo(expected.Map));
            Assert.That(actual.Fingerprint, Is.EqualTo(expected.Fingerprint));
            CollectionAssert.AreEqual(expected.Furniture, actual.Furniture);
            CollectionAssert.AreEqual(expected.Scenery, actual.Scenery);
            CollectionAssert.AreEqual(expected.Clearings, actual.Clearings);
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                Assert.That(actual.IsBlocked(x, y), Is.EqualTo(expected.IsBlocked(x, y)));
        }
    }
}
