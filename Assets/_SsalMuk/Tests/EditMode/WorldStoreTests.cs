using System;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class WorldStoreTests
    {
        [Test]
        public void TerrainEvictionPreservesEveryUnitAndExperienceRecord()
        {
            using var world = new WorldStore(new UnitRegistry(Guid.NewGuid()), new ChunkGenerator(42, MapSettings.TestDefaults()));
            var positions = new[] { new WorldPosition(new ChunkCoord(-50000, 7), new DVec2(16, 16)), WorldPosition.FromLocal(new DVec2(16, 16)) };
            foreach (UnitKind kind in new[] { UnitKind.Normal, UnitKind.Air, UnitKind.Boss }) Spawn(world, kind, positions[0]);
            long a = world.AddExperience(positions[0], BigInteger.Pow(10, 30));
            long b = world.AddExperience(positions[1], 5);
            world.TryBeginAttraction(world.Units.RunId, a);
            world.MoveExperience(world.Units.RunId, a, positions[0].Offset(new DVec2(1, 0)));
            string terrain = world.GetChunk(positions[0].Chunk).Fingerprint;
            world.ClearTerrainCache();
            Assert.That(world.CachedChunkCount, Is.Zero);
            Assert.That(world.Units.Count, Is.EqualTo(3));
            Assert.That(world.Experience.Count, Is.EqualTo(2));
            Assert.That(world.TryGetExperience(a, out var xp), Is.True);
            Assert.That(xp.State, Is.EqualTo(ExperienceState.Attracting));
            Assert.That(xp.Position, Is.EqualTo(positions[0].Offset(new DVec2(1, 0))));
            Assert.That(xp.Value, Is.EqualTo(BigInteger.Pow(10, 30)));
            Assert.That(world.TryGetExperience(b, out var grounded), Is.True);
            Assert.That(grounded.State, Is.EqualTo(ExperienceState.Grounded));
            Assert.That(world.GetChunk(positions[0].Chunk).Fingerprint, Is.EqualTo(terrain));
            Assert.That(world.Query.QueryExperienceCircle(xp.Position, 0.01), Does.Contain(a));
            Assert.That(world.Query.QueryExperienceCircle(positions[0], 0.01), Has.No.Member(a));
        }

        [Test]
        public void NearestSearchCrossesChunksAndMovementUpdatesBothIndexes()
        {
            using var world = new WorldStore(new UnitRegistry(Guid.NewGuid()), new ChunkGenerator(7, MapSettings.TestDefaults(0)));
            var origin = new WorldPosition(new ChunkCoord(9007199254740993, -3), new DVec2(31.9, 16));
            Spawn(world, UnitKind.Player, origin);
            var close = Spawn(world, UnitKind.Normal, origin.Offset(new DVec2(0.2, 0)));
            var far = Spawn(world, UnitKind.Air, origin.Offset(new DVec2(-50, 0)));
            Assert.That(world.Query.FindNearestEnemy(origin), Is.EqualTo(close.Id));
            world.MoveUnit(close.Id, origin.Offset(new DVec2(300, 0)));
            Assert.That(world.Query.FindNearestEnemy(origin), Is.EqualTo(far.Id));
            Assert.That(world.Query.QueryCircle(origin, 1), Has.No.Member(close.Id));
            world.Units.Remove(far.Id);
            Assert.That(world.Query.FindNearestEnemy(origin), Is.EqualTo(close.Id));
            world.Units.Remove(close.Id);
            Assert.That(world.Query.FindNearestEnemy(origin), Is.Null);
        }

        [Test]
        public void EquidistantNearestUsesStableIdAndEmptyIndexTerminates()
        {
            var index = new SpatialIndex();
            var origin = WorldPosition.FromLocal(new DVec2(-0.1, -32));
            Assert.That(index.FindNearest(origin), Is.Null);
            index.Upsert(9, origin.Offset(new DVec2(10, 0)));
            index.Upsert(2, origin.Offset(new DVec2(-10, 0)));
            Assert.That(index.FindNearest(origin), Is.EqualTo(2));
            CollectionAssert.AreEquivalent(new long[] { 2, 9 }, index.QueryCircle(origin, 10));
            Assert.That(index.QueryCircle(origin, 9.999), Is.Empty);
            index.Remove(2); index.Remove(9);
            Assert.That(index.OccupiedChunkCount, Is.Zero);
        }

        [Test]
        public void CollectingIsExplicitAndIdIsNotReused()
        {
            using var world = new WorldStore(new UnitRegistry(Guid.NewGuid()), new ChunkGenerator(2, MapSettings.TestDefaults(0)));
            long id = world.AddExperience(default, 10);
            world.TryBeginAttraction(world.Units.RunId, id);
            world.TryGetExperience(id, out var record);
            Assert.That(world.TryCollectExperience(world.Units.RunId, id, out var value), Is.True);
            Assert.That(value, Is.EqualTo(new BigInteger(10)));
            Assert.That(record.State, Is.EqualTo(ExperienceState.Collected));
            Assert.That(world.TryCollectExperience(world.Units.RunId, id, out _), Is.False);
            Assert.That(world.Experience, Is.Empty);
            Assert.That(world.Query.QueryExperienceCircle(default, 1), Is.Empty);
            Assert.That(world.AddExperience(default, 1), Is.GreaterThan(id));
        }

        [Test]
        public void LocalAndHugeQueriesAgreeWithAllRetainedPositionsAtIntegerWorldEdges()
        {
            foreach (var coord in new[] { default(ChunkCoord), new ChunkCoord(long.MaxValue, long.MinValue), new ChunkCoord(long.MinValue, long.MaxValue) })
            {
                var index = new SpatialIndex();
                var positions = new System.Collections.Generic.Dictionary<long, WorldPosition>();
                var origin = new WorldPosition(coord, new DVec2(31.9, .1)); long id = 0;
                foreach (double x in new[] { -40d, -32, -.2, 0, .2, 32, 40 })
                    foreach (double y in new[] { -40d, -.2, 0, .2, 40 })
                        try { positions.Add(++id, origin.Offset(new DVec2(x, y))); } catch (OverflowException) { }
                for (int i = 4; i < 30; i++)
                    positions.Add(++id, new WorldPosition(new ChunkCoord(coord.X >= 0 ? coord.X - i : coord.X + i,
                        coord.Y >= 0 ? coord.Y - i : coord.Y + i), new DVec2(16, 16)));
                foreach (var pair in positions) index.Upsert(pair.Key, pair.Value);
                foreach (double radius in new[] { 0d, .15, 1, 32, 32.0001, double.MaxValue })
                {
                    var expected = positions.Where(p => p.Key % 2 == 0 && origin.DistanceTo(p.Value) <= radius).Select(p => p.Key).OrderBy(x => x).ToArray();
                    CollectionAssert.AreEqual(expected, index.QueryCircle(origin, radius, candidate => candidate % 2 == 0));
                }
            }
        }

        [Test]
        public void DenseCircleQueriesKeepExactMembershipAcrossCellsMovementAndRemoval()
        {
            foreach (var center in new[] { default(ChunkCoord), new ChunkCoord(-100, -33), new ChunkCoord(long.MaxValue - 1, long.MinValue + 1) })
            {
                var index = new SpatialIndex();
                var positions = new System.Collections.Generic.Dictionary<long, WorldPosition>(); long id = 0;
                for (int cy = -1; cy <= 1; cy++) for (int cx = -1; cx <= 1; cx++)
                    for (int y = 0; y < 32; y += 2) for (int x = 0; x < 32; x += 2)
                    {
                        var position = new WorldPosition(new ChunkCoord(center.X + cx, center.Y + cy), new DVec2(x, y));
                        positions.Add(++id, position); index.Upsert(id, position);
                    }
                for (int phase = 0; phase < 2; phase++)
                {
                    foreach (double local in new[] { 0d, .1, 1, 15.5, 31.999999999999996 })
                        foreach (double radius in new[] { 0d, 1e-12, .26, .52, 1.01, 2.5, 4, 16, 32, 32.000001, 1000 })
                        {
                            var origin = new WorldPosition(center, new DVec2(local, local == 0 ? 0 : 32 - local));
                            var expected = positions.Where(p => p.Key % 3 != 0 && origin.DistanceTo(p.Value) <= radius)
                                .Select(p => p.Key).OrderBy(candidate => candidate).ToArray();
                            CollectionAssert.AreEqual(expected, index.QueryCircle(origin, radius, candidate => candidate % 3 != 0));
                        }
                    foreach (long key in positions.Keys.ToArray())
                        if (key % 7 == 0) { index.Remove(key); positions.Remove(key); }
                        else if (key % 11 == 0) { positions[key] = positions[key].Offset(new DVec2(.125, .25)); index.Upsert(key, positions[key]); }
                }
            }
        }

        internal static UnitModel Spawn(WorldStore world, UnitKind kind, WorldPosition position)
        {
            var definition = new UnitDefinition(kind.ToString(), kind, 10, 2, kind == UnitKind.Boss ? 0.75 : 0.26, 1, BigInteger.One);
            UnitFactory factory = kind == UnitKind.Player ? (UnitFactory)new PlayerFactory(world.Units) :
                kind == UnitKind.Air ? new AirEnemyFactory(world.Units) : new GroundEnemyFactory(world.Units);
            return factory.Spawn(new UnitSpawnRequest(world.Units.RunId, kind, position, definition));
        }
    }
}
