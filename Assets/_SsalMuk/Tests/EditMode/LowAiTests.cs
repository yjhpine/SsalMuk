using System;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class LowAiTests
    {
        [Test]
        public void EncirclementCommitsToAnEscapeDirection()
        {
            using var rig = RunTestRig.Create();
            Surround(rig);
            rig.Ai.Tick(0.02);
            Assert.That(rig.Player.BrainState, Is.EqualTo(BrainState.Breakout));
            var direction = rig.Player.BreakoutDirection;
            Assert.That(direction.Length, Is.EqualTo(1).Within(1e-6));
            for (int i = 0; i < 12; i++) rig.Ai.Tick(0.02);
            Assert.That(rig.Player.BreakoutDirection, Is.EqualTo(direction));
            var target = rig.Unit(rig.Player.TargetId.Value);
            Assert.That(DVec2.Dot(rig.Player.Position.DisplacementTo(target.Position).Normalized, direction), Is.GreaterThan(0.7));
        }

        [Test]
        public void CollectApproachesValuableExperienceDespiteNonImminentRisk()
        {
            using var rig = RunTestRig.Create();
            rig.DropXp(new DVec2(-2, 0), 1);
            long valuable = rig.DropXp(new DVec2(4, 0), 20);
            rig.Spawn(UnitKind.Normal, new DVec2(4, 1));
            rig.Advance(0.2);
            Assert.That(rig.Player.BrainState, Is.EqualTo(BrainState.Collect));
            Assert.That(rig.Player.CollectionTargetId, Is.EqualTo(valuable));
            Assert.That(rig.Player.Position.DisplacementTo(WorldPosition.FromLocal(DVec2.Zero)).X, Is.LessThan(-0.1));
        }

        [Test]
        public void ImminentCollisionOverridesExperienceAndMovesAway()
        {
            using var rig = RunTestRig.Create();
            rig.Spawn(UnitKind.Normal, new DVec2(0.7, 0));
            rig.DropXp(new DVec2(3, 0), 100);
            rig.Ai.Tick(0.02);
            Assert.That(rig.Player.BrainState, Is.EqualTo(BrainState.Evade));
            Assert.That(rig.Player.MoveIntent.X, Is.LessThan(0));
        }

        [Test]
        public void AttractingExperienceIsReleasedAndHugeValuesAreComparedWithoutOverflow()
        {
            using var rig = RunTestRig.Create();
            long best = rig.DropXp(new DVec2(4, 0), BigInteger.Pow(10, 400));
            long next = rig.DropXp(new DVec2(-3, 0), BigInteger.Pow(10, 400) - 100);
            rig.Ai.Tick(0.02);
            Assert.That(rig.Player.CollectionTargetId, Is.EqualTo(best));
            rig.World.TryBeginAttraction(rig.Run.Id, best); rig.Ai.Tick(0.02);
            Assert.That(rig.Player.CollectionTargetId, Is.EqualTo(next));
            Assert.That(rig.Player.MoveIntent.X, Is.LessThan(0));
        }

        [Test]
        public void EscapingEncirclementReturnsToNearestTargetAfterStableOpening()
        {
            using var rig = RunTestRig.Create(); Surround(rig); rig.Ai.Tick(0.02);
            Assert.That(rig.Player.BrainState, Is.EqualTo(BrainState.Breakout));
            foreach (var unit in rig.World.Units.Units.Where(unit => unit.Kind != UnitKind.Player).ToArray()) rig.World.Units.Remove(unit.Id);
            long enemy = rig.Spawn(UnitKind.Normal, new DVec2(-6, 0));
            for (int i = 0; i < 40; i++) rig.Ai.Tick(0.02);
            Assert.That(rig.Player.BrainState, Is.EqualTo(BrainState.Collect));
            Assert.That(rig.Player.TargetId, Is.EqualTo(enemy));
        }

        private static void Surround(RunTestRig rig)
        {
            for (int i = 0; i < 16; i++)
            {
                double angle = i * Math.PI / 8;
                rig.Spawn(UnitKind.Normal, new DVec2(Math.Cos(angle), Math.Sin(angle)));
            }
        }

        [Test]
        public void ShortProtectedAirCrossingDoesNotInterruptCollection()
        {
            using var rig = RunTestRig.Create();
            long air = rig.Spawn(UnitKind.Air, new DVec2(0.8, 0)); rig.Movement.SetAirDirection(air, new DVec2(-1, 0));
            long loot = rig.DropXp(new DVec2(4, 0), 20);
            Assert.That(rig.Damage.TryApply(DamageTests.Request(rig, rig.Player.Id, 1), 0), Is.True);
            rig.Ai.Tick(0.02);
            Assert.That(rig.Player.BrainState, Is.EqualTo(BrainState.Collect));
            Assert.That(rig.Player.CollectionTargetId, Is.EqualTo(loot));
        }

        [Test]
        public void NoProgressReconsidersTheCommittedDirection()
        {
            using var rig = RunTestRig.Create(); Surround(rig); rig.Ai.Tick(0.02);
            var first = rig.Player.BreakoutDirection;
            // Deliberately do not run movement: this is the blocked-by-a-crowd input to the FSM.
            for (int i = 0; i < 50; i++) rig.Ai.Tick(0.02);
            Assert.That(rig.Player.BrainState, Is.EqualTo(BrainState.Breakout));
            Assert.That(DVec2.Dot(first, rig.Player.BreakoutDirection), Is.LessThan(0.99));
        }

        [Test]
        public void UnreachableValuableOrbDoesNotBlockAnAccessibleAlternative()
        {
            using var world = NavigationTests.Layout((x, y) =>
                ((x == 6 || x == 10) && y >= 6 && y <= 10) || ((y == 6 || y == 10) && x >= 6 && x <= 10));
            var player = (PlayerModel)WorldStoreTests.Spawn(world, UnitKind.Player, NavigationTests.P(3.5, 8.5));
            var navigation = new NavigationService(world); var ai = new LowAiController(player, world, navigation);
            world.AddExperience(NavigationTests.P(8.5, 8.5), 1000);
            long accessible = world.AddExperience(NavigationTests.P(3.5, 5.5), 2);
            for (int i = 0; i < 100; i++) { ai.Tick(0.02); navigation.Advance(1024); }
            Assert.That(player.CollectionTargetId, Is.EqualTo(accessible));
            Assert.That(player.MoveIntent.Y, Is.LessThan(0));
        }

        [Test]
        public void ThreatNearWallChoosesAFreeDirection()
        {
            using var world = NavigationTests.Layout((x, y) => x == 5);
            var player = (PlayerModel)WorldStoreTests.Spawn(world, UnitKind.Player, NavigationTests.P(4.5, 4.5));
            WorldStoreTests.Spawn(world, UnitKind.Normal, NavigationTests.P(3.8, 4.5));
            var ai = new LowAiController(player, world, new NavigationService(world)); ai.Tick(0.02);
            Assert.That(player.MoveIntent.Length, Is.GreaterThan(0));
            Assert.That(world.Query.SweepCircle(player.Position, player.MoveIntent, player.BodyRadius), Is.Null);
        }

        [Test]
        public void ApproachUsesTheSameWeightedBossTargetAsAttacks()
        {
            using var rig = RunTestRig.Create(enableCombat: false);
            rig.Spawn(UnitKind.Normal, new DVec2(-3, 0));
            long boss = rig.Spawn(UnitKind.Boss, new DVec2(6, 0));
            rig.Ai.Tick(.02);
            Assert.That(rig.Player.BrainState, Is.EqualTo(BrainState.Collect));
            Assert.That(rig.Player.TargetId, Is.EqualTo(boss));
            Assert.That(rig.Player.MoveIntent.X, Is.GreaterThan(.9));
        }

        [TestCase(1, false)]
        [TestCase(7, false)]
        [TestCase(8, true)]
        public void ReachableExperienceAccumulationCanOutweighBossApproach(int count, bool collect)
        {
            using var rig = RunTestRig.Create(enableCombat: false);
            long boss = rig.Spawn(UnitKind.Boss, new DVec2(6, 0));
            for (int i = 0; i < count; i++) rig.DropXp(new DVec2(-4, i * .1), 1);
            rig.Ai.Tick(.02);
            Assert.That(rig.Player.BrainState, Is.EqualTo(BrainState.Collect));
            Assert.That(rig.Player.TargetId, Is.EqualTo(boss));
            Assert.That(rig.Player.CollectionTargetId.HasValue, Is.EqualTo(collect));
            if (collect) Assert.That(rig.Player.MoveIntent.X, Is.LessThan(-.9));
            else Assert.That(rig.Player.MoveIntent.X, Is.GreaterThan(.9));
        }

        [Test]
        public void UnreachableExperienceHoardDoesNotOutweighBossApproach()
        {
            using var world = NavigationTests.Layout((x, y) =>
                ((x == 6 || x == 10) && y >= 6 && y <= 10) || ((y == 6 || y == 10) && x >= 6 && x <= 10));
            var player = (PlayerModel)WorldStoreTests.Spawn(world, UnitKind.Player, NavigationTests.P(3.5, 8.5));
            var boss = WorldStoreTests.Spawn(world, UnitKind.Boss, NavigationTests.P(3.5, 13.5));
            var navigation = new NavigationService(world); var ai = new LowAiController(player, world, navigation);
            for (int i = 0; i < 8; i++) world.AddExperience(NavigationTests.P(8.5, 8.5), 1000);
            world.AddExperience(NavigationTests.P(3.5, 5.5), 1);
            for (int i = 0; i < 100; i++) { ai.Tick(.02); navigation.Advance(1024); }
            Assert.That(player.BrainState, Is.EqualTo(BrainState.Collect));
            Assert.That(player.TargetId, Is.EqualTo(boss.Id));
            Assert.That(player.CollectionTargetId, Is.Null);
            Assert.That(player.MoveIntent.Y, Is.GreaterThan(.9));
        }

        [Test]
        public void NearbyItemIsApproachedWithoutUsingItsIdentityAsAnExperienceTarget()
        {
            using var rig = RunTestRig.Create(enableCombat: false);
            long boss = rig.Spawn(UnitKind.Boss, new DVec2(6, 0));
            rig.World.AddItem(PowerupKind.MoveSpeed, WorldPosition.FromLocal(new DVec2(-4, 0)));
            rig.Ai.Tick(.02);
            Assert.That(rig.Player.TargetId, Is.EqualTo(boss));
            Assert.That(rig.Player.CollectionTargetId, Is.Null);
            Assert.That(rig.Player.MoveIntent.X, Is.LessThan(-.9));
        }

        [Test]
        public void CloserAbundantExperienceKeepsPriorityOverADistantItem()
        {
            using var rig = RunTestRig.Create(enableCombat: false);
            rig.Spawn(UnitKind.Boss, new DVec2(0, 8));
            rig.World.AddItem(PowerupKind.AttackRange, WorldPosition.FromLocal(new DVec2(-8, 0)));
            long xp = rig.DropXp(new DVec2(3, 0), 8);
            rig.Ai.Tick(.02);
            Assert.That(rig.Player.CollectionTargetId, Is.EqualTo(xp));
            Assert.That(rig.Player.MoveIntent.X, Is.GreaterThan(.9));
        }

        [Test]
        public void UnreachableItemDoesNotHideReachableExperienceWithTheSameIdentity()
        {
            using var world = NavigationTests.Layout((x, y) =>
                ((x == 6 || x == 10) && y >= 6 && y <= 10) || ((y == 6 || y == 10) && x >= 6 && x <= 10));
            var player = (PlayerModel)WorldStoreTests.Spawn(world, UnitKind.Player, NavigationTests.P(3.5, 8.5));
            var navigation = new NavigationService(world); var ai = new LowAiController(player, world, navigation);
            world.AddItem(PowerupKind.Invincibility, NavigationTests.P(8.5, 8.5));
            long xp = world.AddExperience(NavigationTests.P(3.5, 5.5), 2);
            for (int i = 0; i < 100; i++) { ai.Tick(.02); navigation.Advance(1024); }
            Assert.That(player.CollectionTargetId, Is.EqualTo(xp));
            Assert.That(player.MoveIntent.Y, Is.LessThan(-.9));
        }

        [Test]
        public void ImminentContactOverridesNearbyItemSeeking()
        {
            using var rig = RunTestRig.Create(enableCombat: false);
            rig.World.AddItem(PowerupKind.Invincibility, WorldPosition.FromLocal(new DVec2(3, 0)));
            rig.Spawn(UnitKind.Normal, new DVec2(.7, 0));
            rig.Ai.Tick(.02);
            Assert.That(rig.Player.BrainState, Is.EqualTo(BrainState.Evade));
            Assert.That(rig.Player.MoveIntent.X, Is.LessThan(0));
        }
    }
}
