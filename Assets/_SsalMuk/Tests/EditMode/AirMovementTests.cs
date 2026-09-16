using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class AirMovementTests
    {
        [Test]
        public void EnemyFsmTransitionsFromChaseThroughKnockbackAndDeath()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            long id = rig.Spawn(UnitKind.Normal, new DVec2(4, 4), 100);
            var fsm = rig.Movement.GetEnemyFsm(id); Assert.That(fsm, Is.Not.Null);
            Assert.That(fsm.CurrentState, Is.EqualTo(EnemyState.Chase));
            var before = rig.Unit(id).Position;
            for (int i = 0; i < 10 && rig.Unit(id).Position == before; i++) rig.Movement.Step(0.02);
            Assert.That(rig.Unit(id).Position.DistanceTo(rig.Player.Position), Is.LessThan(before.DistanceTo(rig.Player.Position)));
            rig.Hit(id, 1); rig.Movement.Step(0.02); Assert.That(fsm.CurrentState, Is.EqualTo(EnemyState.Knockback));
            Assert.That(fsm.MoveIntent, Is.EqualTo(DVec2.Zero));
            rig.Hit(id, 1000); var deadAt = rig.Unit(id).Position; rig.Movement.Step(0.02);
            Assert.That(fsm.CurrentState, Is.EqualTo(EnemyState.Dead)); Assert.That(rig.Unit(id).Position, Is.EqualTo(deadAt));
            rig.Death.Flush(); Assert.That(fsm.CurrentState, Is.EqualTo(EnemyState.Dead));
        }
        [Test]
        public void AirKnockbackTakesPriorityThenResumesItsStoredDirection()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            long id = rig.Spawn(UnitKind.Air, new DVec2(3, 1), 100); rig.Movement.SetAirDirection(id, new DVec2(-1, 0));
            var fsm = rig.Movement.GetEnemyFsm(id); Assert.That(fsm, Is.Not.Null);
            Assert.That(fsm.CurrentState, Is.EqualTo(EnemyState.FlyThrough));
            rig.Hit(id, 1); rig.Movement.Step(0.1);
            Assert.That(fsm.CurrentState, Is.EqualTo(EnemyState.Knockback)); Assert.That(rig.Unit(id).Position.Local.X, Is.EqualTo(3.2).Within(1e-8));
            rig.Movement.Step(0.05); var after = rig.Unit(id).Position;
            Assert.That(fsm.CurrentState, Is.EqualTo(EnemyState.FlyThrough));
            rig.PlacePlayer(new DVec2(10, 10)); rig.Movement.Step(0.2);
            Assert.That(after.DisplacementTo(rig.Unit(id).Position).X, Is.EqualTo(-1.2).Within(1e-8));
            Assert.That(((AirEnemyModel)rig.Unit(id)).OriginalDirection, Is.EqualTo(new DVec2(-1, 0)));
        }
        [Test]
        public void ALongMovementStepStillUsesTheFlightTimeAfterKnockbackEnds()
        {
            using var rig = RunTestRig.Create(enableAi: false); long id = rig.Spawn(UnitKind.Air, new DVec2(3, 1), 100);
            rig.Movement.SetAirDirection(id, new DVec2(-1, 0)); rig.Hit(id, 1); rig.Movement.Step(1);
            Assert.That(rig.Unit(id).Position.DisplacementTo(WorldPosition.FromLocal(new DVec2(-1.8, 1))).Length, Is.LessThan(1e-8));
        }
    }
}
