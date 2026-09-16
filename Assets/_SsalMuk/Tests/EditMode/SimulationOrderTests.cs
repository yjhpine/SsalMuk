using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class SimulationOrderTests
    {
        [Test]
        public void SwordKillPrecedesContactAndDropsExperienceInTheSameTick()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            long enemy = rig.Spawn(UnitKind.Normal, new DVec2(0.3, 0), 1);
            var simulation = new RunSimulation(rig.Run, rig.Movement, null, rig.Damage, rig.Death, rig.Contact);
            simulation.Step(0.02);
            Assert.That(rig.World.Units.TryGet(enemy, out _), Is.False);
            Assert.That(rig.Run.Kills, Is.EqualTo(1)); Assert.That(rig.World.Experience.Count, Is.EqualTo(1));
            Assert.That(rig.Player.Health, Is.EqualTo(100));
        }

        [Test]
        public void MovementAndRecentNonlethalHitDoNotInterruptAutomaticAttacks()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            long enemy = rig.Spawn(UnitKind.Normal, new DVec2(1.2, 0), 100);
            rig.Movement.SetMoveIntent(enemy, DVec2.Zero);
            rig.Movement.SetMoveIntent(rig.Player.Id, new DVec2(1, 0));
            var position = rig.Player.Position; Assert.That(rig.Hit(rig.Player.Id, 1), Is.True);
            rig.Advance(0.2);
            Assert.That(rig.Unit(enemy).Health, Is.EqualTo(92));
            Assert.That(rig.Player.Health, Is.EqualTo(99));
            Assert.That(rig.Player.Position.DistanceTo(position), Is.GreaterThan(0.2));
        }

        [Test]
        public void PlayerCanAttackWhileStillAndADeadPlayerStopsFurtherSimulation()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            long enemy = rig.Spawn(UnitKind.Normal, new DVec2(1, 0), 100);
            rig.Movement.SetMoveIntent(enemy, DVec2.Zero);
            var simulation = new RunSimulation(rig.Run, rig.Movement, null, rig.Damage, rig.Death, rig.Contact);
            var position = rig.Player.Position; simulation.Step(0.2);
            Assert.That(rig.Unit(enemy).Health, Is.EqualTo(92)); Assert.That(rig.Player.Position, Is.EqualTo(position));
            rig.Hit(rig.Player.Id, 1000); double time = rig.Clock.ElapsedSeconds; simulation.Step(1);
            Assert.That(rig.Run.Phase, Is.EqualTo(RunPhase.Results)); Assert.That(rig.Clock.ElapsedSeconds, Is.EqualTo(time));
            Assert.That(rig.Unit(enemy).Health, Is.EqualTo(92));
        }
    }
}
