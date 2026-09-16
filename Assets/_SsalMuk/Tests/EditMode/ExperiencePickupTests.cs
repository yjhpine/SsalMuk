using System;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class ExperiencePickupTests
    {
        [Test]
        public void ProximityStartsFlightButOnlyBodyContactPaysOnce()
        {
            using var rig = RunTestRig.Create(enableAi: false); long id = rig.DropXp(new DVec2(1.2, 0), 1);
            rig.Advance(0.02);
            Assert.That(rig.World.TryGetExperience(id, out var orb), Is.True);
            Assert.That(orb.State, Is.EqualTo(ExperienceState.Attracting));
            Assert.That(orb.Position.Local.X, Is.LessThan(1.2));
            Assert.That(rig.Player.Growth.ExperienceIntoLevel, Is.EqualTo(BigInteger.Zero));
            rig.Advance(1); Assert.That(rig.World.TryGetExperience(id, out _), Is.False);
            Assert.That(rig.Player.Growth.ExperienceIntoLevel, Is.EqualTo(BigInteger.One));
            rig.Advance(1); Assert.That(rig.Player.Growth.ExperienceIntoLevel, Is.EqualTo(BigInteger.One));
        }
        [Test]
        public void AnAttractingOrbKeepsFollowingAfterThePlayerLeavesTheRadius()
        {
            using var rig = RunTestRig.Create(enableAi: false, pickupSettings: new PickupSettings(flightSpeed: 1));
            long id = rig.DropXp(new DVec2(-1.2, 0), 1); rig.Advance(0.02);
            rig.Movement.SetMoveIntent(rig.Player.Id, new DVec2(1, 0)); rig.Advance(1);
            Assert.That(rig.World.TryGetExperience(id, out var orb), Is.True);
            Assert.That(orb.State, Is.EqualTo(ExperienceState.Attracting));
            Assert.That(orb.Position.DistanceTo(rig.Player.Position), Is.GreaterThan(1.5));
            Assert.That(orb.Position.DistanceTo(WorldPosition.FromLocal(new DVec2(-1.2, 0))), Is.GreaterThan(0.9));
            rig.Movement.SetMoveIntent(rig.Player.Id, DVec2.Zero); rig.Advance(4);
            Assert.That(rig.Player.Growth.ExperienceIntoLevel, Is.EqualTo(BigInteger.One));
        }
        [Test]
        public void ContinuousContactFindsAPlayerPassingAnOrbBetweenTwoPositions()
        {
            using var rig = RunTestRig.Create(enableAi: false, pickupSettings: new PickupSettings(flightSpeed: 0.01));
            rig.DropXp(new DVec2(1.5, 0), 1); rig.Movement.SetMoveIntent(rig.Player.Id, new DVec2(1, 0));
            rig.Movement.Step(1); rig.Clock.Advance(50); rig.Simulation.Collector.Step(1);
            Assert.That(rig.Player.Position.Local.X, Is.EqualTo(3).Within(1e-8));
            Assert.That(rig.Player.Growth.ExperienceIntoLevel, Is.EqualTo(BigInteger.One));
        }
        [Test]
        public void NewlyCreatedOrbCannotBeCollectedByAnEarlierMovementSegment()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            rig.Movement.SetMoveIntent(rig.Player.Id, new DVec2(1, 0)); rig.Movement.Step(1); rig.Clock.Advance(50);
            long id = rig.DropXp(new DVec2(1.5, 0), 5); rig.Simulation.Collector.Step(1);
            Assert.That(rig.World.TryGetExperience(id, out var orb), Is.True);
            Assert.That(orb.Position.Local.X, Is.EqualTo(1.5)); Assert.That(orb.State, Is.EqualTo(ExperienceState.Attracting));
            Assert.That(rig.Player.Growth.ExperienceIntoLevel, Is.EqualTo(BigInteger.Zero));
        }
        [Test]
        public void AttractionCrossesWallsAndMovesTheSpatialIndexAcrossChunks()
        {
            using var rig = RunTestRig.Create(enableAi: false, terrain: new VerticalWall());
            rig.PlacePlayer(new DVec2(33.5, 0.5)); long id = rig.DropXp(new DVec2(30.5, 0.5), 1);
            Assert.That(rig.World.TryBeginAttraction(rig.Run.Id, id), Is.True);
            rig.Advance(0.3); Assert.That(rig.World.TryGetExperience(id, out var orb), Is.True);
            Assert.That(orb.Position.Chunk.X, Is.EqualTo(1));
            Assert.That(rig.World.Query.QueryExperienceCircle(orb.Position, 0.01), Does.Contain(id));
            Assert.That(rig.World.Query.QueryExperienceCircle(WorldPosition.FromLocal(new DVec2(30.5, 0.5)), 0.01), Has.No.Member(id));
            rig.Advance(1); Assert.That(rig.Player.Growth.ExperienceIntoLevel, Is.EqualTo(BigInteger.One));
        }
        [Test]
        public void WrongRunGroundedAndRepeatedCollectionCannotGrantAnOrb()
        {
            using var rig = RunTestRig.Create(enableAi: false); long id = rig.DropXp(new DVec2(3, 0), 25);
            Assert.That(rig.World.TryCollectExperience(rig.Run.Id, id, out _), Is.False);
            Assert.That(rig.World.TryBeginAttraction(Guid.NewGuid(), id), Is.False);
            Assert.That(rig.World.TryBeginAttraction(rig.Run.Id, id), Is.True);
            Assert.That(rig.World.MoveExperience(Guid.NewGuid(), id, default), Is.False);
            Assert.That(rig.World.TryCollectExperience(Guid.NewGuid(), id, out _), Is.False);
            Assert.That(rig.World.TryCollectExperience(rig.Run.Id, id, out var value), Is.True); Assert.That(value, Is.EqualTo(new BigInteger(25)));
            Assert.That(rig.World.TryCollectExperience(rig.Run.Id, id, out _), Is.False);
        }
        [Test]
        public void DeathPrecedesPickupInTheSameStepAndStopsLaterPayouts()
        {
            using var rig = RunTestRig.Create(enableAi: false); rig.DropXp(DVec2.Zero, 100);
            var definition = new UnitDefinition("lethal", UnitKind.Air, 10000, 6, 0.26, 1000, BigInteger.One);
            new AirEnemyFactory(rig.World.Units).Spawn(new UnitSpawnRequest(rig.Run.Id, UnitKind.Air, rig.Player.Position, definition));
            rig.Advance(0.02); Assert.That(rig.Result, Is.Not.Null); Assert.That(rig.Result.FinalLevel, Is.EqualTo(BigInteger.One));
            Assert.That(rig.GrantExperience(100), Is.False); Assert.That(rig.Player.Growth.PendingChoices, Is.EqualTo(BigInteger.Zero));
        }
        [Test]
        public void PickupGradesUseTheUnchangedValueAndRejectInvalidSettings()
        {
            var settings = PickupSettings.TestDefaults();
            Assert.That(settings.TierFor(1), Is.EqualTo(ExperienceTier.Green)); Assert.That(settings.TierFor(4), Is.EqualTo(ExperienceTier.Green));
            Assert.That(settings.TierFor(5), Is.EqualTo(ExperienceTier.Blue)); Assert.That(settings.TierFor(24), Is.EqualTo(ExperienceTier.Blue));
            Assert.That(settings.TierFor(BigInteger.Pow(10, 400)), Is.EqualTo(ExperienceTier.Red));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PickupSettings(flightSpeed: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PickupSettings(blueThreshold: 25, redThreshold: 25));
            Assert.Throws<ArgumentException>(() => new PickupSettings(attractionRadius: 0.3).ValidateForPlayer(0.28));
        }
        private sealed class VerticalWall : IChunkGenerator
        {
            public ChunkData Generate(ChunkCoord coord)
            {
                var blocked = new bool[1024]; if (coord.Equals(default(ChunkCoord))) for (int y = 0; y < 32; y++) blocked[y * 32 + 31] = true;
                return new ChunkData(coord, blocked, 1);
            }
        }
        [Test]
        public void AiUsesTheConfiguredAttractionDistanceAndReleasesTheFlyingTarget()
        {
            using var rig = RunTestRig.Create(pickupSettings: new PickupSettings(attractionRadius: 0.45));
            rig.DropXp(new DVec2(2, 0), 1); rig.Advance(3);
            Assert.That(rig.Player.Growth.ExperienceIntoLevel, Is.EqualTo(BigInteger.One));
            Assert.That(rig.Player.CollectionTargetId, Is.Null);
            Assert.That(rig.Player.MoveIntent, Is.EqualTo(DVec2.Zero));
        }
        [Test]
        public void AirDeathInsideAnObstacleDropsTheFullValueOnTheNearestFreeFloor()
        {
            using var rig = RunTestRig.Create(enableAi: false, terrain: new VerticalWall());
            rig.Clock.Advance(50);
            long air = rig.Spawn(UnitKind.Air, new DVec2(31.7, 0.5));
            rig.Hit(air, 100); rig.Death.Flush(); rig.Death.Flush();
            var orb = rig.World.Experience.Single();
            Assert.That(orb.Value, Is.EqualTo(BigInteger.One)); Assert.That(orb.State, Is.EqualTo(ExperienceState.Grounded));
            Assert.That(rig.World.Query.IsCircleFree(orb.Position, rig.Player.BodyRadius), Is.True);
            Assert.That(orb.Position, Is.EqualTo(WorldPosition.FromLocal(new DVec2(32.5, 0.5))));
            Assert.That(orb.CreatedAt, Is.EqualTo(1));
            rig.PlacePlayer(new DVec2(-5000, -5000)); rig.Advance(1);
            Assert.That(rig.World.Experience.Single().Id, Is.EqualTo(orb.Id));
            Assert.That(rig.World.Experience.Single().Position, Is.EqualTo(orb.Position));
        }
    }
}
