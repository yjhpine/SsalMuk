using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class BossChargeTests
    {
        [TestCase(0, false), TestCase(2, false), TestCase(0, true), TestCase(20, true)]
        public void ChargeSpeedIsThirtyRegardlessOfPlayerUpgradesAndItems(int upgrades, bool boost)
        {
            using var rig = RunTestRig.Create(enableAi: false);
            if (upgrades > 0) new GrowthService(rig.Run).UpgradeShared(SharedUpgradeKind.MoveSpeed, upgrades);
            if (boost) { rig.World.AddItem(PowerupKind.MoveSpeed, rig.Player.Position); rig.Advance(.02); }
            rig.PlacePlayer(new DVec2(3, 0));
            var boss = rig.Unit(rig.Spawn(UnitKind.Boss, DVec2.Zero, 1000));
            rig.Movement.Step(.5); var start = boss.Position; rig.Movement.Step(.1);
            Assert.That(start.DisplacementTo(boss.Position).X, Is.EqualTo(3).Within(1e-8));
        }

        [TestCase(.1), TestCase(3.625), TestCase(100)]
        public void ChargeSpeedDoesNotScaleOrRoundWithTheTargetSpeed(double playerSpeed)
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            var boss = rig.Unit(rig.Spawn(UnitKind.Boss, DVec2.Zero));
            var target = new SpeedTarget(playerSpeed);
            var charge = new BossCharge(new FixedRandom(1));
            charge.TryMove(boss, target, .5, out _); charge.TryMove(boss, target, .1, out var move);
            Assert.That(move.X, Is.EqualTo(3).Within(1e-9));
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
                Assert.That(start.DisplacementTo(boss.Position).X, Is.EqualTo(.6).Within(1e-8));
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
        public void CompletedChargeSequenceRestoresKnockback()
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            rig.PlacePlayer(new DVec2(3, 0));
            var boss = (GroundEnemyModel)rig.Unit(rig.Spawn(UnitKind.Boss, DVec2.Zero, 1000));
            rig.Movement.Step(.5); rig.Movement.Step(.5);
            for (int i = 0; i < 100 && boss.Charge.HasSuperArmor; i++) rig.Movement.Step(.02);
            Assert.That(boss.Position.DistanceTo(rig.Player.Position), Is.LessThan(1e-8));
            Assert.That(boss.Charge.HasSuperArmor, Is.False);
            Assert.That(boss.Charge.Phase, Is.EqualTo(EnemyState.Chase));
            Assert.That(rig.Hit(boss.Id, 1), Is.True);
            Assert.That(boss.Knockback.IsActive, Is.True);
            rig.Movement.Step(.02);
            Assert.That(rig.Movement.GetEnemyFsm(boss.Id).CurrentState, Is.EqualTo(EnemyState.Knockback));
        }

        [TestCase(.299999, true)]
        [TestCase(.3, false)]
        public void RandomPassesAlwaysEndWithOneLockedChargeToTheLatestPlayerPosition(double roll, bool repeats)
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            rig.PlacePlayer(new DVec2(3, 0));
            var boss = rig.Unit(rig.Spawn(UnitKind.Boss, DVec2.Zero));
            var target = new SharedPlayerPosition(); target.Refresh(rig.Player);
            var random = new FixedRandom(roll); var charge = new BossCharge(random);
            Assert.That(charge.TryMove(boss, target, .5, out var wait), Is.True);
            Assert.That(wait, Is.EqualTo(DVec2.Zero));
            Assert.That(charge.Length, Is.EqualTo(6));
            Assert.That(charge.End, Is.EqualTo(WorldPosition.FromLocal(new DVec2(6, 0))));
            charge.TryMove(boss, target, 1, out var first);
            Assert.That(first.X, Is.EqualTo(6).Within(1e-9));
            rig.World.MoveUnit(boss.Id, boss.Position.Offset(first));
            Assert.That(charge.HasSuperArmor, Is.True, "The next leg cannot be cancelled by an intervening hit.");
            if (repeats)
            {
                rig.PlacePlayer(new DVec2(4, 1)); target.Refresh(rig.Player);
                Assert.That(charge.TryMove(boss, target, .5, out var extraWait), Is.True);
                Assert.That(extraWait, Is.EqualTo(DVec2.Zero));
                Assert.That(charge.End, Is.EqualTo(WorldPosition.FromLocal(new DVec2(2, 2))));
                charge.TryMove(boss, target, .01, out var extraStart);
                Assert.That(extraStart.Length, Is.EqualTo(.3).Within(1e-9));
                rig.World.MoveUnit(boss.Id, boss.Position.Offset(extraStart));
                charge.TryMove(boss, target, 1, out var second);
                rig.World.MoveUnit(boss.Id, boss.Position.Offset(second));
                Assert.That(boss.Position.DistanceTo(WorldPosition.FromLocal(new DVec2(2, 2))), Is.LessThan(1e-9));
            }
            rig.PlacePlayer(new DVec2(1, 2)); target.Refresh(rig.Player);
            var destination = rig.Player.Position; double finalDistance = boss.Position.DistanceTo(destination);
            Assert.That(charge.TryMove(boss, target, .25, out var approachWait), Is.True);
            Assert.That(approachWait, Is.EqualTo(DVec2.Zero));
            Assert.That(charge.Phase, Is.EqualTo(EnemyState.Telegraph));
            Assert.That(charge.End, Is.EqualTo(destination));
            Assert.That(charge.Length, Is.EqualTo(finalDistance).Within(1e-9));
            rig.PlacePlayer(new DVec2(20, -8)); target.Refresh(rig.Player);
            charge.TryMove(boss, target, .25, out var remainingWait);
            Assert.That(remainingWait, Is.EqualTo(DVec2.Zero));
            Assert.That(charge.End, Is.EqualTo(destination), "The final warning locks the endpoint until this charge ends.");
            charge.TryMove(boss, target, .01, out var approachStart);
            Assert.That(approachStart.Length, Is.EqualTo(.3).Within(1e-9));
            rig.World.MoveUnit(boss.Id, boss.Position.Offset(approachStart));
            charge.TryMove(boss, target, 1, out var approachEnd);
            rig.World.MoveUnit(boss.Id, boss.Position.Offset(approachEnd));
            Assert.That(boss.Position.DistanceTo(destination), Is.LessThan(1e-9));
            Assert.That(random.Calls, Is.EqualTo(1), "Only the optional second pass rolls a probability.");
            Assert.That(charge.HasSuperArmor, Is.False);
            Assert.That(charge.TryMove(boss, target, 4.99, out _), Is.False);
            Assert.That(charge.TryMove(boss, target, .01, out _), Is.False);
            Assert.That(charge.TryMove(boss, target, .02, out _), Is.True);
            Assert.That(charge.Phase, Is.EqualTo(EnemyState.Telegraph));
            Assert.That(charge.Length, Is.EqualTo(boss.Position.DistanceTo(target.Position) * 2).Within(1e-9));
        }

        [Test]
        public void HitsBetweenPassesDoNotCancelTheFinalApproach()
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            rig.PlacePlayer(new DVec2(3, 0));
            var boss = (GroundEnemyModel)rig.Unit(rig.Spawn(UnitKind.Boss, DVec2.Zero, 1000));
            rig.Movement.Step(.5); rig.Movement.Step(1);
            Assert.That(boss.Position.Local.X, Is.EqualTo(6).Within(1e-9));
            Assert.That(boss.Charge.HasSuperArmor, Is.True);
            Assert.That(rig.Hit(boss.Id, 7), Is.True);
            Assert.That(boss.Health, Is.EqualTo(993));
            Assert.That(boss.Knockback.IsActive, Is.False);
            for (int i = 0; i < 100 && boss.Charge.HasSuperArmor; i++) rig.Movement.Step(.02);
            Assert.That(boss.Position.DistanceTo(rig.Player.Position), Is.LessThan(1e-8));
            Assert.That(boss.Charge.HasSuperArmor, Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void CancellingOrAlreadyReachingTheFinalDestinationEndsTheSequence(bool alreadyThere)
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            rig.PlacePlayer(new DVec2(3, 0));
            var boss = rig.Unit(rig.Spawn(UnitKind.Boss, DVec2.Zero));
            var target = new SharedPlayerPosition(); target.Refresh(rig.Player);
            var charge = new BossCharge(new FixedRandom(1));
            charge.TryMove(boss, target, .5, out _); charge.TryMove(boss, target, 1, out var first);
            rig.World.MoveUnit(boss.Id, boss.Position.Offset(first));
            if (alreadyThere)
            {
                rig.World.MoveUnit(rig.Player.Id, boss.Position); target.Refresh(rig.Player);
                Assert.That(charge.TryMove(boss, target, .02, out var zero), Is.False);
                Assert.That(zero, Is.EqualTo(DVec2.Zero));
            }
            else
            {
                Assert.That(charge.TryMove(boss, target, .1, out _), Is.True);
                Assert.That(charge.End, Is.EqualTo(target.Position));
                charge.Cancel();
            }
            Assert.That(charge.HasSuperArmor, Is.False);
            rig.PlacePlayer(new DVec2(8, 0)); target.Refresh(rig.Player);
            Assert.That(charge.TryMove(boss, target, 5, out _), Is.False);
            Assert.That(charge.TryMove(boss, target, .02, out _), Is.True);
            Assert.That(charge.Length, Is.EqualTo(4).Within(1e-9), "After cancellation the next sequence starts with a full pass.");
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
            charge.TryMove(boss, target, .02, out var crossing); Assert.That(crossing.X, Is.EqualTo(.3).Within(1e-9));
            charge.TryMove(boss, target, 10, out var final); Assert.That(final.X, Is.EqualTo(5.7).Within(1e-9));
        }

        [Test]
        public void TelegraphIgnoresRepeatedKnockbackKeepsItsWarningAndStillAllowsDeath()
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            rig.PlacePlayer(new DVec2(3, 0));
            long id = rig.Spawn(UnitKind.Boss, DVec2.Zero, 1000);
            rig.Movement.Step(.02);
            var boss = (GroundEnemyModel)rig.Unit(id);
            Assert.That(boss.Charge.Phase, Is.EqualTo(EnemyState.Telegraph));
            var origin = boss.Position; var end = boss.Charge.End; double health = boss.Health;
            for (int i = 0; i < 24; i++)
            {
                Assert.That(rig.Hit(id, 1), Is.True);
                rig.Movement.AddKnockback(id, new DVec2(-1, 0), .1);
                Assert.That(boss.Knockback.IsActive, Is.False);
                rig.Movement.Step(.02);
                Assert.That(boss.Position, Is.EqualTo(origin));
                Assert.That(boss.Charge.End, Is.EqualTo(end));
                Assert.That(boss.Charge.Phase, Is.EqualTo(i == 23 ? EnemyState.Charge : EnemyState.Telegraph));
            }
            Assert.That(boss.Health, Is.EqualTo(health - 24));
            Assert.That(boss.HitSequence, Is.EqualTo(24));
            rig.Movement.Step(.02);
            Assert.That(origin.DisplacementTo(boss.Position).X, Is.EqualTo(.6).Within(1e-9));
            rig.Hit(id, 10000); var position = boss.Position; rig.Movement.Step(.02);
            Assert.That(boss.Position, Is.EqualTo(position));
            Assert.That(rig.Movement.GetEnemyFsm(id).CurrentState, Is.EqualTo(EnemyState.Dead));
        }

        [Test]
        public void LethalDamageDuringTelegraphStillPreventsCharge()
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            rig.PlacePlayer(new DVec2(3, 0));
            var boss = (GroundEnemyModel)rig.Unit(rig.Spawn(UnitKind.Boss, DVec2.Zero));
            rig.Movement.Step(.02); var origin = boss.Position;
            rig.Hit(boss.Id, 10000); rig.Movement.Step(1);
            Assert.That(boss.IsAlive, Is.False);
            Assert.That(boss.Position, Is.EqualTo(origin));
            Assert.That(rig.Movement.GetEnemyFsm(boss.Id).CurrentState, Is.EqualTo(EnemyState.Dead));
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
        public void RepeatedHitsLetCooldownElapseAndChargeResumesAfterKnockback(bool finishSequence)
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            rig.PlacePlayer(new DVec2(3, 0));
            long id = rig.Spawn(UnitKind.Boss, DVec2.Zero, 10000);
            var boss = (GroundEnemyModel)rig.Unit(id);
            rig.Movement.Step(.02);
            Assert.That(boss.Charge.Phase, Is.EqualTo(EnemyState.Telegraph));
            if (finishSequence)
            {
                rig.Movement.Step(.48); rig.Movement.Step(1);
                Assert.That(boss.Position.Local.X, Is.EqualTo(6).Within(1e-9));
                for (int i = 0; i < 100 && boss.Charge.HasSuperArmor; i++) rig.Movement.Step(.02);
                Assert.That(boss.Position.DistanceTo(rig.Player.Position), Is.LessThan(1e-9));
                Assert.That(boss.Charge.Phase, Is.EqualTo(EnemyState.Chase));
            }
            else boss.Charge.Cancel(); // A terrain cancellation still starts the ordinary cooldown.
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

        private sealed class SpeedTarget : IPlayerPosition
        {
            public bool IsAlive => true;
            public WorldPosition Position => WorldPosition.FromLocal(new DVec2(10, 0));
            public double MoveSpeed { get; }
            public SpeedTarget(double speed) => MoveSpeed = speed;
        }
        private sealed class FixedRandom : IRandomSource
        {
            private readonly double value;
            public int Calls { get; private set; }
            public FixedRandom(double value) => this.value = value;
            public double NextUnit() { Calls++; return value; }
        }
    }
}
