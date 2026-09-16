using System.Linq;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class SpawnIntegrationTests
    {
        [Test]
        public void OnlyOptedInRigsSpawnFromTheirRunningSimulationAndRestartHasAFreshSchedule()
        {
            using (var manual = RunTestRig.Create(enableAi: false))
            {
                manual.Advance(1.02); Assert.That(manual.Run.Units.Count, Is.EqualTo(1));
            }
            using var rig = RunTestRig.Create(scheduledSpawns: true, enableAi: false);
            rig.Advance(1.02);
            var spawned = rig.Run.Units.Single(x => x.Kind == UnitKind.Normal);
            Assert.That(new WorldRect(rig.Player.Position, 16, 9).Contains(spawned.Position, spawned.BodyRadius), Is.False);
            Assert.That(rig.World.Query.IsCircleFree(spawned.Position, spawned.BodyRadius), Is.True);
            var oldRun = rig.Run; var oldSimulation = rig.Simulation;
            rig.Hit(rig.Player.Id, 1000); rig.Advance(0.02); rig.RestartAsync().GetAwaiter().GetResult();
            Assert.That(rig.Run.Id, Is.Not.EqualTo(oldRun.Id)); Assert.That(oldRun.Units.Count, Is.Zero);
            oldSimulation.Step(1); Assert.That(rig.Run.Units.Count, Is.EqualTo(1));
            rig.Advance(1.02); Assert.That(rig.Run.Units.Count, Is.EqualTo(2));
        }

        [Test]
        public void SimultaneousNormalAirAndBossContactsRespectOnePlayerInvulnerabilityWindow()
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            foreach (var kind in new[] { UnitKind.Normal, UnitKind.Air, UnitKind.Boss })
            {
                var id = rig.Spawn(kind, new DVec2(0.1, 0), 100);
                if (kind != UnitKind.Air) rig.Movement.SetMoveIntent(id, DVec2.Zero);
            }
            rig.Movement.Step(0.02); rig.Contact.Step(0, 0.02);
            Assert.That(rig.Player.Health, Is.EqualTo(95)); Assert.That(rig.Player.HitSequence, Is.EqualTo(1));
            rig.Contact.Step(0.02, 0.04); Assert.That(rig.Player.Health, Is.EqualTo(95));
        }
    }
}
