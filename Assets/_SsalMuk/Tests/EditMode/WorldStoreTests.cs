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

        internal static UnitModel Spawn(WorldStore world, UnitKind kind, WorldPosition position)
        {
            var definition = new UnitDefinition(kind.ToString(), kind, 10, 2, kind == UnitKind.Boss ? 0.75 : 0.26, 1, BigInteger.One);
            UnitFactory factory = kind == UnitKind.Player ? (UnitFactory)new PlayerFactory(world.Units) :
                kind == UnitKind.Air ? new AirEnemyFactory(world.Units) : new GroundEnemyFactory(world.Units);
            return factory.Spawn(new UnitSpawnRequest(world.Units.RunId, kind, position, definition));
        }
    }
}
