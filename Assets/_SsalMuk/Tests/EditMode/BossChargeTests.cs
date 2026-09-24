using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class BossChargeTests
    {
        [TestCase(0, false), TestCase(2, false), TestCase(0, true), TestCase(2, true)]
        public void ChargeUsesFourTimesTheUpgradedAndBoostedPlayerSpeed(int upgrades, bool boost)
        {
            using var rig = RunTestRig.Create(enableAi: false);
            if (upgrades > 0) new GrowthService(rig.Run).UpgradeShared(SharedUpgradeKind.MoveSpeed, upgrades);
            if (boost) { rig.World.AddItem(PowerupKind.MoveSpeed, rig.Player.Position); rig.Advance(.02); }
            rig.PlacePlayer(new DVec2(3, 0));
            var boss = rig.Unit(rig.Spawn(UnitKind.Boss, DVec2.Zero, 1000));
            double speed = 3 * (1 + .1 * upgrades) * (boost ? 1.5 : 1);
            rig.Movement.Step(.5); var start = boss.Position; rig.Movement.Step(.1);
            Assert.That(start.DisplacementTo(boss.Position).X, Is.EqualTo(speed * 4 * .1).Within(1e-8));
        }

        [Test]
        public void ChargingBossTakesRepeatedDamageWithoutKnockbackOrInterruption()
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            rig.PlacePlayer(new DVec2(5, 0));
            var boss = (GroundEnemyModel)rig.Unit(rig.Spawn(UnitKind.Boss, DVec2.Zero, 1000));
            rig.Movement.Step(.5);
            Assert.That(boss.Charge.Phase, Is.EqualTo(EnemyState.Charge));
            for (int i = 0; i < 5; i++)
            {
                Assert.That(rig.Hit(boss.Id, 1), Is.True);
                Assert.That(boss.Knockback.IsActive, Is.False);
                var start = boss.Position; rig.Movement.Step(.02);
                Assert.That(start.DisplacementTo(boss.Position).X, Is.EqualTo(.24).Within(1e-8));
                Assert.That(boss.Charge.Phase, Is.EqualTo(EnemyState.Charge));
            }
            Assert.That(boss.Health, Is.EqualTo(995));
            Assert.That(boss.HitSequence, Is.EqualTo(5));
            rig.Movement.AddKnockback(boss.Id, new DVec2(-4, 0), .5);
            Assert.That(boss.Knockback.IsActive, Is.False);
            Assert.That(rig.Hit(boss.Id, 2000), Is.True);
            var position = boss.Position; rig.Movement.Step(.02);
            Assert.That(boss.IsAlive, Is.False);
            Assert.That(boss.Position, Is.EqualTo(position));
            Assert.That(rig.Movement.GetEnemyFsm(boss.Id).CurrentState, Is.EqualTo(EnemyState.Dead));
        }

        [Test]
        public void CompletedChargeRestoresKnockback()
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            rig.PlacePlayer(new DVec2(3, 0));
            var boss = (GroundEnemyModel)rig.Unit(rig.Spawn(UnitKind.Boss, DVec2.Zero, 1000));
            rig.Movement.Step(.5); rig.Movement.Step(.5);
            Assert.That(boss.Position.Local.X, Is.EqualTo(6).Within(1e-8));
            Assert.That(boss.Charge.Phase, Is.EqualTo(EnemyState.Chase));
            Assert.That(rig.Hit(boss.Id, 1), Is.True);
            Assert.That(boss.Knockback.IsActive, Is.True);
            rig.Movement.Step(.02);
            Assert.That(rig.Movement.GetEnemyFsm(boss.Id).CurrentState, Is.EqualTo(EnemyState.Knockback));
        }

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
            charge.TryMove(boss, target, .02, out var crossing); Assert.That(crossing.X, Is.EqualTo(.12).Within(1e-9));
            charge.TryMove(boss, target, 10, out var final); Assert.That(final.X, Is.EqualTo(5.88).Within(1e-9));
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

        [TestCase(false)]
        [TestCase(true)]
        public void RepeatedHitsLetCooldownElapseAndChargeResumesAfterKnockback(bool finishFirstCharge)
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            rig.PlacePlayer(new DVec2(3, 0));
            long id = rig.Spawn(UnitKind.Boss, DVec2.Zero, 10000);
            var boss = (GroundEnemyModel)rig.Unit(id);
            rig.Movement.Step(.02);
            Assert.That(boss.Charge.Phase, Is.EqualTo(EnemyState.Telegraph));
            if (finishFirstCharge)
            {
                rig.Movement.Step(.48); rig.Movement.Step(1);
                Assert.That(boss.Position.Local.X, Is.EqualTo(6).Within(1e-9));
                Assert.That(boss.Charge.Phase, Is.EqualTo(EnemyState.Chase));
            }
            var beforeHits = boss.Position;
            // Real accepted damage refreshes the .15s push every .1s for six seconds.
            for (int i = 0; i < 300; i++)
            {
                if (i % 5 == 0) Assert.That(rig.Hit(id, .01), Is.True);
                rig.Movement.Step(.02);
                Assert.That(boss.Charge.Phase, Is.EqualTo(EnemyState.Chase));
                Assert.That(rig.Movement.GetEnemyFsm(id).CurrentState, Is.EqualTo(EnemyState.Knockback));
            }
            Assert.That(boss.Health, Is.EqualTo(9999.4).Within(1e-7));
            Assert.That(beforeHits.DisplacementTo(boss.Position).X, Is.GreaterThan(1));
            rig.Movement.Step(.1); Assert.That(boss.Knockback.IsActive, Is.False);
            rig.Movement.Step(.02);
            Assert.That(boss.Charge.Phase, Is.EqualTo(EnemyState.Telegraph), "Repeated hits must not restart the five-second cooldown.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WallOnTheFinalChargeStepCancelsTheQueuedExtraCharge(bool blocked)
        {
            using var world = NavigationTests.Layout((x, y) => blocked && x == 5);
            var player = (PlayerModel)WorldStoreTests.Spawn(world, UnitKind.Player, NavigationTests.P(9.5, 4.5));
            var boss = (GroundEnemyModel)WorldStoreTests.Spawn(world, UnitKind.Boss, NavigationTests.P(2.5, 4.5));
            // Seed zero queues an extra charge for boss ID 2; the open layout is the control.
            using var movement = new MovementSystem(world, new NavigationService(world), player, seed: 0);
            movement.Step(.5); movement.Step(4);
            Assert.That(boss.Charge.Phase, Is.EqualTo(EnemyState.Chase));
            if (blocked) Assert.That(boss.Position.Local.X, Is.LessThan(5));
            else Assert.That(boss.Position.Local.X, Is.EqualTo(16.5).Within(1e-9));
            movement.Step(.02);
            Assert.That(boss.Charge.Phase, Is.EqualTo(blocked ? EnemyState.Chase : EnemyState.Telegraph));
        }

        private sealed class FixedRandom : IRandomSource
        {
            private readonly double value;
            public FixedRandom(double value) => this.value = value;
            public double NextUnit() => value;
        }
    }
}
