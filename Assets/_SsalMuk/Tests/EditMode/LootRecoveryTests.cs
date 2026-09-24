using System;
using System.Linq;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class LootRecoveryTests
    {
        [Test]
        public void DenseNearbyAreaWinsOverASingleMoreValuableOrb()
        {
            using var rig = RunTestRig.Create(enableCombat: false);
            rig.DropXp(new DVec2(-4, 1), 5);
            long first = rig.DropXp(new DVec2(4, 1), 1);
            for (int i = 1; i < 12; i++) rig.DropXp(new DVec2(4 + i * .03, 1), 1);
            rig.Ai.Tick(.02);
            Assert.That(rig.Player.CollectionTargetId, Is.GreaterThanOrEqualTo(first));
            Assert.That(rig.Player.MoveIntent.X, Is.GreaterThan(.8));
        }

        [Test]
        public void PursuitRemembersLootOutsideLocalSearchAndFlanksTheHerd()
        {
            using var rig = RunTestRig.Create(enableCombat: false);
            long loot = rig.DropXp(new DVec2(4, 0), 30);
            rig.Ai.Tick(.02);
            long threat = rig.Spawn(UnitKind.Normal, new DVec2(.7, 0));
            rig.Ai.Tick(.02);
            Assert.That(rig.Player.BrainState, Is.EqualTo(BrainState.Evade));
            // The player has been pushed far down one corridor; the same herd follows behind.
            rig.PlacePlayer(new DVec2(-13, 0));
            rig.World.MoveUnit(threat, WorldPosition.FromLocal(new DVec2(-9, 0)));
            for (int i = 0; i < 40; i++) rig.Ai.Tick(.02);
            Assert.That(rig.Player.CollectionTargetId, Is.EqualTo(loot));
            Assert.That(Math.Abs(rig.Player.MoveIntent.Y), Is.GreaterThan(.5), "Circle the herd rather than reverse through it.");
            double turn = Math.Sign(rig.Player.MoveIntent.Y);
            for (int i = 0; i < 12; i++)
            {
                rig.Ai.Tick(.02);
                Assert.That(Math.Sign(rig.Player.MoveIntent.Y), Is.EqualTo(turn));
            }
            rig.World.Units.Remove(threat);
            for (int i = 0; i < 20; i++) rig.Ai.Tick(.02);
            Assert.That(rig.Player.MoveIntent.X, Is.GreaterThan(.9));
            rig.World.TryBeginAttraction(rig.Run.Id, loot);
            rig.Ai.Tick(.02);
            Assert.That(rig.Player.CollectionTargetId, Is.Null);
            Assert.That(rig.Player.MoveIntent, Is.EqualTo(DVec2.Zero));
        }

        [Test]
        public void NewThreatInterruptsCachedCollectionImmediately()
        {
            using var rig = RunTestRig.Create(enableCombat: false);
            rig.DropXp(new DVec2(4, 0), 30);
            rig.Ai.Tick(.02);
            rig.Spawn(UnitKind.Normal, new DVec2(.7, 0));
            rig.Ai.Tick(.02);
            Assert.That(rig.Player.BrainState, Is.EqualTo(BrainState.Evade));
            Assert.That(rig.Player.MoveIntent.X, Is.LessThan(0));
        }

        [Test]
        public void BossWindupLaneInterruptsCollectionBeforePhysicalContact()
        {
            using var rig = RunTestRig.Create(enableCombat: false);
            long boss = rig.Spawn(UnitKind.Boss, new DVec2(-5, 0), 1000);
            rig.DropXp(new DVec2(5, 0), 30);
            rig.Movement.Step(.02);
            Assert.That(((GroundEnemyModel)rig.Unit(boss)).Charge.Phase, Is.EqualTo(EnemyState.Telegraph));
            rig.Ai.Tick(.02);
            Assert.That(rig.Player.BrainState, Is.EqualTo(BrainState.Evade));
            Assert.That(Math.Abs(rig.Player.MoveIntent.Y), Is.GreaterThan(.7));
        }

        [Test]
        public void ChasingHerdIsLuredAsideAndExperienceIsActuallyCollected()
        {
            using var rig = RunTestRig.Create(enableCombat: false);
            long loot = rig.DropXp(new DVec2(4, 0), 1);
            rig.Ai.Tick(.02);
            for (int i = 0; i < 9; i++) rig.Spawn(UnitKind.Normal, new DVec2(.7 + i / 3 * .65, (i % 3 - 1) * .65), 1000);
            var collector = new ExperienceCollector(rig.Run, rig.Movement, new ProgressionService(rig.Run));
            double minX = 0, maxY = 0; bool flanked = false;
            for (int i = 0; i < 1500 && rig.World.TryGetExperience(loot, out _); i++)
            {
                rig.Advance(.02); collector.Step(.02);
                var position = WorldPosition.FromLocal(DVec2.Zero).DisplacementTo(rig.Player.Position);
                minX = Math.Min(minX, position.X); maxY = Math.Max(maxY, Math.Abs(position.Y));
                flanked |= rig.Ai.CollectionTactic == CollectionTactic.Flank;
            }
            Assert.That(minX, Is.LessThan(-2), "First make space while being chased.");
            Assert.That(flanked, Is.True);
            Assert.That(maxY, Is.GreaterThan(2), "Return around the herd, not straight through it.");
            Assert.That(rig.World.TryGetExperience(loot, out _), Is.False, "Return must finish at actual absorption/contact, not only change state.");
            Assert.That(rig.Player.Growth.ExperienceIntoLevel, Is.EqualTo(System.Numerics.BigInteger.One));
        }

        [Test]
        public void ThousandsOfOrbsShareAreaRiskAndPlanningIsThrottled()
        {
            using var rig = RunTestRig.Create(enableCombat: false);
            for (int i = 0; i < 2000; i++) rig.DropXp(new DVec2(4.1 + i % 20 * .01, 1.1 + i % 10 * .01), 1);
            for (int i = 0; i < 10; i++) rig.Ai.Tick(.02);
            Assert.That(rig.Ai.CollectionPlanCount, Is.EqualTo(1));
            Assert.That(rig.Ai.CollectionAreaRiskEvaluations, Is.EqualTo(1));
            rig.Ai.Tick(.02);
            Assert.That(rig.Ai.CollectionPlanCount, Is.EqualTo(2));
            Assert.That(rig.Ai.CollectionAreaRiskEvaluations, Is.EqualTo(2));
            Assert.That(rig.Run.Experience.Count, Is.EqualTo(2000), "Planning must not merge or delete actual loot.");
        }

        [Test]
        public void InaccessibleHoardInTheSameAreaCannotBorrowTheSmallReachableOrbsPriority()
        {
            using var world = NavigationTests.Layout((x, y) =>
                ((x == 9 || x == 11) && y >= 9 && y <= 11) || ((y == 9 || y == 11) && x >= 9 && x <= 11));
            var player = (PlayerModel)WorldStoreTests.Spawn(world, UnitKind.Player, NavigationTests.P(7, 10.5));
            var boss = WorldStoreTests.Spawn(world, UnitKind.Boss, NavigationTests.P(7, 16.5));
            world.AddExperience(NavigationTests.P(10.5, 10.5), 1000);
            world.AddExperience(NavigationTests.P(8.5, 10.5), 1);
            var navigation = new NavigationService(world); var ai = new LowAiController(player, world, navigation);
            for (int i = 0; i < 30; i++) { ai.Tick(.02); navigation.Advance(1024); }
            Assert.That(player.CollectionTargetId, Is.Null);
            Assert.That(player.TargetId, Is.EqualTo(boss.Id));
            Assert.That(player.MoveIntent.Y, Is.GreaterThan(.9));
        }

        [Test]
        public void RegionScoringRemainsAccurateAtHugeChunkCoordinates()
        {
            using var rig = RunTestRig.Create(enableCombat: false);
            var origin = new WorldPosition(new ChunkCoord(1000000000000, -1000000000000), new DVec2(31.8, 16));
            rig.World.MoveUnit(rig.Player.Id, origin);
            long loot = rig.World.AddExperience(origin.Offset(new DVec2(4, 0)), System.Numerics.BigInteger.Pow(10, 400));
            rig.Ai.Tick(.02);
            Assert.That(rig.Player.CollectionTargetId, Is.EqualTo(loot));
            Assert.That(rig.Player.MoveIntent.X, Is.GreaterThan(.99));
        }

        [Test]
        public void LootFirstDroppedDuringAnEscapeIsRememberedForTheReturn()
        {
            using var rig = RunTestRig.Create(enableCombat: false);
            long threat = rig.Spawn(UnitKind.Normal, new DVec2(.6, 0));
            rig.Ai.Tick(.02);
            Assert.That(rig.Player.BrainState, Is.EqualTo(BrainState.Evade));
            long loot = rig.DropXp(new DVec2(3, 0), 30);
            for (int i = 0; i < 15; i++) rig.Ai.Tick(.02);
            Assert.That(rig.Ai.RememberedExperienceId, Is.EqualTo(loot));
            rig.PlacePlayer(new DVec2(-13, 0));
            rig.World.MoveUnit(threat, WorldPosition.FromLocal(new DVec2(-9, 0)));
            for (int i = 0; i < 40; i++) rig.Ai.Tick(.02);
            Assert.That(rig.Player.CollectionTargetId, Is.EqualTo(loot));
        }

        [Test]
        public void UnreachableAreaValueDoesNotInflateThePriorityOfItsAccessibleOrb()
        {
            using var world = NavigationTests.Layout((x, y) =>
                ((x == 9 || x == 11) && y >= 9 && y <= 11) || ((y == 9 || y == 11) && x >= 9 && x <= 11));
            var player = (PlayerModel)WorldStoreTests.Spawn(world, UnitKind.Player, NavigationTests.P(7, 10.5));
            world.AddExperience(NavigationTests.P(10.5, 10.5), 1000);
            world.AddExperience(NavigationTests.P(8.5, 10.5), 1);
            long valuable = world.AddExperience(NavigationTests.P(7, 6.5), 20);
            var navigation = new NavigationService(world); var ai = new LowAiController(player, world, navigation);
            for (int i = 0; i < 30; i++) { ai.Tick(.02); navigation.Advance(1024); }
            Assert.That(player.CollectionTargetId, Is.EqualTo(valuable));
        }

        [Test]
        public void ReturningToVeryDistantLootOnlyInspectsLocalTerrainSteps()
        {
            using var rig = RunTestRig.Create(enableCombat: false);
            long loot = rig.DropXp(new DVec2(4, 0), 30); rig.Ai.Tick(.02);
            long threat = rig.Spawn(UnitKind.Normal, new DVec2(.7, 0)); rig.Ai.Tick(.02);
            rig.World.Units.Remove(threat); rig.PlacePlayer(new DVec2(-1000, 0));
            for (int i = 0; i < 40; i++) rig.Ai.Tick(.02);
            Assert.That(rig.Player.CollectionTargetId, Is.EqualTo(loot));
            Assert.That(rig.Player.MoveIntent.X, Is.GreaterThan(.99));
            Assert.That(rig.World.CachedChunkCount, Is.LessThan(20), "Do not generate every chunk on a distant return route each tick.");
        }
    }
}
