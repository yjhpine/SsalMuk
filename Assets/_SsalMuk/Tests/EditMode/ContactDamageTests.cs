using System.Collections.Generic;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class ContactDamageTests
    {
        [Test]
        public void FastAirCrossingBetweenFramesStillHurtsThePlayer()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            long air = rig.Spawn(UnitKind.Air, new DVec2(-2, 0)); rig.Movement.SetAirDirection(air, new DVec2(1, 0));
            rig.Movement.Step(1); Assert.That(rig.Unit(air).Position.Local.X, Is.EqualTo(4));
            rig.Contact.Step(0, 1); Assert.That(rig.Player.Health, Is.EqualTo(95));
        }

        [Test]
        public void SimultaneousContactsUseIdentityOrderAndShortInvulnerability()
        {
            using var rig = RunTestRig.Create(enableAi: false); var sources = new List<long>();
            long first = rig.Spawn(UnitKind.Air, DVec2.Zero); rig.Spawn(UnitKind.Air, DVec2.Zero);
            rig.Damage.Accepted += hit => sources.Add(hit.Request.SourceId);
            rig.Movement.Step(0.02); rig.Contact.Step(0, 0.02);
            Assert.That(sources, Is.EqualTo(new[] { first })); Assert.That(rig.Player.Health, Is.EqualTo(95));
            rig.Contact.Step(0.24, 0.26);
            Assert.That(rig.Player.Health, Is.EqualTo(90));
        }

        [Test]
        public void EarlierContactWinsEvenWhenItsIdentityIsLarger()
        {
            using var rig = RunTestRig.Create(enableAi: false); var sources = new List<long>();
            rig.Spawn(UnitKind.Air, new DVec2(-2, 0)); long early = rig.Spawn(UnitKind.Air, new DVec2(-1, 0));
            rig.Damage.Accepted += hit => sources.Add(hit.Request.SourceId);
            rig.Movement.Step(0.5); rig.Contact.Step(0, 0.5);
            Assert.That(sources, Is.Not.Empty);
            Assert.That(sources[0], Is.EqualTo(early));
        }

        [Test]
        public void EnemyKilledDuringAttackPhaseCannotDealContactDamage()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            long enemy = rig.Spawn(UnitKind.Air, DVec2.Zero); rig.Movement.Step(0.02);
            Assert.That(rig.Hit(enemy, 100), Is.True); rig.Contact.Step(0, 0.02);
            Assert.That(rig.Player.Health, Is.EqualTo(100));
        }

        [Test]
        public void SustainedContactRespectsEveryInvulnerabilityBoundaryInALongStep()
        {
            using var rig = RunTestRig.Create(enableAi: false); rig.Spawn(UnitKind.Air, DVec2.Zero);
            // Stationary contact throughout this interval: no movement phase has occurred.
            rig.Contact.Step(0, 0.76);
            Assert.That(rig.Player.Health, Is.EqualTo(80));
            Assert.That(rig.Player.HitSequence, Is.EqualTo(4));
            rig.Contact.Step(0, 0.76);
            Assert.That(rig.Player.Health, Is.EqualTo(80));
        }
    }
}
