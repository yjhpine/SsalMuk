using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class BossChargeTests
    {
        [TestCase(.299999, true)]
        [TestCase(.3, false)]
        public void RepeatThresholdAllowsOnlyOneExtraChargeThenFiveSecondsOfCooldown(double roll, bool repeats)
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            rig.PlacePlayer(new DVec2(3, 0));
            var boss = rig.Unit(rig.Spawn(UnitKind.Boss, DVec2.Zero));
            var target = new SharedPlayerPosition(); target.Refresh(rig.Player);
            var charge = new BossCharge(new FixedRandom(roll));
            Assert.That(charge.TryMove(boss, target, .5, out var wait), Is.True);
            Assert.That(wait, Is.EqualTo(DVec2.Zero));
            Assert.That(charge.Length, Is.EqualTo(6));
            Assert.That(charge.End, Is.EqualTo(WorldPosition.FromLocal(new DVec2(6, 0))));
            charge.TryMove(boss, target, 1, out var first);
            Assert.That(first.X, Is.EqualTo(6).Within(1e-9));
            rig.World.MoveUnit(boss.Id, boss.Position.Offset(first));
            Assert.That(charge.TryMove(boss, target, .5, out var extraWait), Is.EqualTo(repeats));
            Assert.That(extraWait, Is.EqualTo(DVec2.Zero));
            if (repeats)
            {
                charge.TryMove(boss, target, 1, out var second);
                Assert.That(second.X, Is.EqualTo(-6).Within(1e-9));
                rig.World.MoveUnit(boss.Id, boss.Position.Offset(second));
            }
            // The no-repeat branch already spent .5 seconds of its cooldown above.
            Assert.That(charge.TryMove(boss, target, repeats ? 5 : 4.5, out _), Is.False);
            Assert.That(charge.TryMove(boss, target, .02, out _), Is.True);
            Assert.That(charge.Phase, Is.EqualTo(EnemyState.Telegraph));
        }

        [Test]
        public void WindupUsesOnlyHalfASecondAndFinalMovementCannotOvershoot()
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            rig.PlacePlayer(new DVec2(3, 0));
            var boss = rig.Unit(rig.Spawn(UnitKind.Boss, DVec2.Zero));
            var target = new SharedPlayerPosition(); target.Refresh(rig.Player);
            var charge = new BossCharge(new FixedRandom(1));
            charge.TryMove(boss, target, .49, out var before); Assert.That(before, Is.EqualTo(DVec2.Zero));
            Assert.That(charge.Phase, Is.EqualTo(EnemyState.Telegraph));
            charge.TryMove(boss, target, .02, out var crossing); Assert.That(crossing.X, Is.EqualTo(.06).Within(1e-9));
            charge.TryMove(boss, target, 10, out var final); Assert.That(final.X, Is.EqualTo(5.94).Within(1e-9));
        }

        [Test]
        public void KnockbackCancelsTheWarningAndDeathPreventsACharge()
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            rig.PlacePlayer(new DVec2(3, 0));
            long id = rig.Spawn(UnitKind.Boss, DVec2.Zero);
            rig.Movement.Step(.02);
            var boss = (GroundEnemyModel)rig.Unit(id);
            Assert.That(boss.Charge.Phase, Is.EqualTo(EnemyState.Telegraph));
            rig.Movement.AddKnockback(id, new DVec2(-1, 0), .1); rig.Movement.Step(.02);
            Assert.That(boss.Charge.Phase, Is.EqualTo(EnemyState.Chase));
            Assert.That(rig.Movement.GetEnemyFsm(id).CurrentState, Is.EqualTo(EnemyState.Knockback));
            rig.Hit(id, 10000); var position = boss.Position; rig.Movement.Step(.02);
            Assert.That(boss.Position, Is.EqualTo(position));
            Assert.That(rig.Movement.GetEnemyFsm(id).CurrentState, Is.EqualTo(EnemyState.Dead));
        }

        [Test]
        public void ChargingBossCannotPassThroughFurniture()
        {
            using var world = NavigationTests.Layout((x, y) => x == 5);
            var player = (PlayerModel)WorldStoreTests.Spawn(world, UnitKind.Player, NavigationTests.P(9.5, 4.5));
            var boss = (GroundEnemyModel)WorldStoreTests.Spawn(world, UnitKind.Boss, NavigationTests.P(2.5, 4.5));
            using var movement = new MovementSystem(world, new NavigationService(world), player);
            for (int i = 0; i < 100; i++) movement.Step(.02);
            Assert.That(boss.Position.Local.X, Is.LessThan(5));
            Assert.That(world.Query.IsCircleFree(boss.Position, boss.BodyRadius), Is.True);
            Assert.That(boss.Charge.Phase, Is.EqualTo(EnemyState.Chase));
        }

        private sealed class FixedRandom : IRandomSource
        {
            private readonly double value;
            public FixedRandom(double value) => this.value = value;
            public double NextUnit() => value;
        }
    }
}
