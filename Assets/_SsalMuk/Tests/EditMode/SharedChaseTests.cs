using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class SharedChaseTests
    {
        [Test]
        public void AllEnemiesUseThePlayerPositionCapturedBeforeMovement()
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            long a = rig.Spawn(UnitKind.Normal, new DVec2(0, 1));
            long b = rig.Spawn(UnitKind.Normal, new DVec2(0, 2));
            rig.Movement.SetMoveIntent(rig.Player.Id, new DVec2(1, 0));
            rig.Movement.Step(.02);
            Assert.That(rig.Player.Position.Local.X, Is.EqualTo(.06).Within(1e-9));
            Assert.That(rig.Unit(a).Position.Local.X, Is.Zero.Within(1e-9));
            Assert.That(rig.Unit(b).Position.Local.X, Is.Zero.Within(1e-9));
            Assert.That(rig.Unit(a).Position.Local.Y, Is.EqualTo(.97).Within(1e-9));
        }

        [Test]
        public void GroundEnemiesDoNotSteerAwayFromOverlappingNeighbors()
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            rig.PlacePlayer(new DVec2(1, 0));
            long a = rig.Spawn(UnitKind.Normal, DVec2.Zero);
            long b = rig.Spawn(UnitKind.Normal, new DVec2(.1, 0));
            rig.Movement.SetMoveIntent(b, DVec2.Zero); rig.Movement.Step(.02);
            Assert.That(rig.Unit(a).Position.Local.X, Is.EqualTo(.03).Within(1e-9));
            Assert.That(rig.Unit(a).Position.Local.Y, Is.Zero.Within(1e-9));
        }

        [Test]
        public void FarEnemiesMoveImmediatelyWithoutRequestingIndividualPaths()
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            long enemy = rig.Spawn(UnitKind.Normal, new DVec2(150, 0));
            var start = rig.Unit(enemy).Position; rig.Movement.Step(.02);
            Assert.That(start.DisplacementTo(rig.Unit(enemy).Position).X, Is.EqualTo(-.03).Within(1e-9));
            Assert.That(rig.Navigation.PendingRequestCount, Is.Zero);
        }

        [Test]
        public void BossWaitsHalfASecondThenChargesAlongTheAdvertisedDirection()
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            rig.PlacePlayer(new DVec2(3, 0));
            long boss = rig.Spawn(UnitKind.Boss, DVec2.Zero);
            for (int i = 0; i < 25; i++) rig.Movement.Step(.02);
            Assert.That(rig.Unit(boss).Position, Is.EqualTo(WorldPosition.FromLocal(DVec2.Zero)));
            rig.PlacePlayer(new DVec2(3, 4)); rig.Movement.Step(.1);
            Assert.That(rig.Unit(boss).Position.Local.X, Is.EqualTo(.6).Within(1e-9));
            Assert.That(rig.Unit(boss).Position.Local.Y, Is.Zero.Within(1e-9));
        }
    }
}
