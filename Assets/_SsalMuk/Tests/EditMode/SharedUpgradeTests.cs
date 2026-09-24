using System;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class SharedUpgradeTests
    {
        [TestCase(1, 1), TestCase(2, 3), TestCase(3, 4), TestCase(4, 5)]
        public void ExperienceRoundsEachOrbHalfUp(int raw, int expected)
        {
            using var rig = RunTestRig.Create(enableAi: false);
            new GrowthService(rig.Run).UpgradeShared(SharedUpgradeKind.Experience);
            rig.DropXp(new DVec2(.3, 0), raw); rig.Advance(.02);
            var earned = rig.Run.GrowthSettings.CostForLevels(1, rig.Player.Level - 1) + rig.Player.Growth.ExperienceIntoLevel;
            Assert.That(earned, Is.EqualTo(new BigInteger(expected)));
        }
        [Test]
        public void ExperienceRoundingRemainsExactForHugeValues()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            new GrowthService(rig.Run).UpgradeShared(SharedUpgradeKind.Experience, 3);
            BigInteger raw = BigInteger.Pow(10, 400) + 2;
            Assert.That(rig.Player.Upgrades.ExperienceFor(raw), Is.EqualTo((raw * 7 + 2) / 4));
        }
        [Test]
        public void SharedMovementPickupAndHealingAffectTheRunningSimulation()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            var upgrades = new GrowthService(rig.Run);
            upgrades.UpgradeShared(SharedUpgradeKind.MoveSpeed, 2);
            upgrades.UpgradeShared(SharedUpgradeKind.PickupRange);
            upgrades.UpgradeShared(SharedUpgradeKind.Regeneration);
            rig.Damage.TryApply(new DamageRequest(new HitKey(rig.Run.Id, rig.Run.AllocateAttackId(), 0, 0),
                rig.Player.Id, rig.Player.Id, 10, DVec2.Zero), rig.Clock.ElapsedSeconds);
            rig.DropXp(new DVec2(rig.Run.PickupSettings.AttractionRadius * 1.1, 0), 1);
            rig.Advance(.02);
            Assert.That(rig.World.Experience.Single().State, Is.EqualTo(ExperienceState.Attracting));
            rig.Movement.SetMoveIntent(rig.Player.Id, new DVec2(0, 1)); var start = rig.Player.Position;
            rig.Advance(1);
            Assert.That(start.DistanceTo(rig.Player.Position), Is.EqualTo(3.6).Within(1e-8));
            Assert.That(rig.Player.Health, Is.EqualTo(90.102).Within(1e-8));
            upgrades.UpgradeShared(SharedUpgradeKind.Regeneration, 10000); rig.Advance(.02);
            Assert.That(rig.Player.Health, Is.EqualTo(100));
        }
        [Test]
        public void DeathAndRestartRemoveAllSharedUpgradesWithoutRevivingThePlayer()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            var upgrades = new GrowthService(rig.Run);
            foreach (SharedUpgradeKind kind in Enum.GetValues(typeof(SharedUpgradeKind))) upgrades.UpgradeShared(kind, 2);
            rig.World.AddItem(PowerupKind.MoveSpeed, rig.Player.Position); rig.Advance(.02);
            Assert.That(rig.Player.MoveSpeed, Is.EqualTo(5.4).Within(1e-8));
            var previous = rig.Player; rig.Hit(previous.Id, 1000); rig.Advance(.02);
            Assert.That(previous.IsAlive, Is.False);
            foreach (SharedUpgradeKind kind in Enum.GetValues(typeof(SharedUpgradeKind)))
                Assert.That(previous.Upgrades.GetLevel(kind), Is.EqualTo(BigInteger.Zero));
            rig.RestartAsync().GetAwaiter().GetResult();
            Assert.That(rig.Player.MoveSpeed, Is.EqualTo(3));
            Assert.That(rig.Player.Upgrades.HealingPerSecond, Is.Zero);
            Assert.That(rig.Player.Upgrades.PickupMultiplier, Is.EqualTo(1));
            Assert.That(rig.Player.Upgrades.ExperienceFor(2), Is.EqualTo(new BigInteger(2)));
        }
        [Test]
        public void SharedChoicesAppearAndOneQueuedClickAppliesOnceWithoutEquippingAWeapon()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            rig.GrantExperience(100000);
            bool chosen = false;
            for (int i = 0; i < 100 && !chosen; i++)
            {
                rig.Advance(.02); var offer = rig.CurrentOffer;
                int slot = offer.Choices.ToList().FindIndex(choice => choice.IsSharedUpgrade);
                if (slot < 0) { rig.Choose(offer.Id, 0); continue; }
                var kind = offer.Choices[slot].SharedKind; var pending = rig.Player.Growth.PendingChoices;
                int weapons = rig.Player.Weapons.Kinds.Count;
                Assert.That(rig.Choose(offer.Id, slot), Is.True); Assert.That(rig.Choose(offer.Id, slot), Is.False);
                rig.Advance(.02);
                Assert.That(rig.Player.Upgrades.GetLevel(kind), Is.EqualTo(BigInteger.One));
                Assert.That(rig.Player.Weapons.Kinds.Count, Is.EqualTo(weapons));
                Assert.That(rig.Player.Growth.PendingChoices, Is.EqualTo(pending - 1));
                chosen = true;
            }
            Assert.That(chosen, Is.True);
            foreach (SharedUpgradeKind kind in Enum.GetValues(typeof(SharedUpgradeKind)))
                Assert.That(Enumerable.Range(1, 100).SelectMany(seed => new OfferGenerator().Generate(
                    new[] { WeaponKind.Sword }, new SeededRandom(seed), RewardWeights.TestDefaults())).Any(choice => choice.Equals(RewardId.Shared(kind))), Is.True);
        }
    }
}
