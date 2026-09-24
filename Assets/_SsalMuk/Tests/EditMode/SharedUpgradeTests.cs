using System;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using SsalMuk.Core;
using SsalMuk.Presentation;

namespace SsalMuk.Tests
{
    public sealed class SharedUpgradeTests
    {
        [TestCase(1, "1.15"), TestCase(2, "2.3"), TestCase(3, "3.45"), TestCase(4, "4.6")]
        public void ExperienceKeepsEachOrbsFifteenPercentBonusAndShowsItsFraction(int raw, string expected)
        {
            using var rig = RunTestRig.Create(enableAi: false);
            new GrowthService(rig.Run).UpgradeShared(SharedUpgradeKind.Experience);
            rig.DropXp(new DVec2(.3, 0), raw); rig.Advance(.02);
            var view = new ExperienceHud(); new HudPresenter(view).Refresh(rig.Run);
            Assert.That(view.Experience, Is.EqualTo("경험치 " + expected + " / 5"));
            Assert.That(view.Fraction, Is.EqualTo(raw * 1.15 / 5).Within(.0001));
        }
        [Test]
        public void ExperienceBonusRemainsExactForHugeValues()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            new GrowthService(rig.Run).UpgradeShared(SharedUpgradeKind.Experience, 3);
            BigInteger raw = BigInteger.Pow(10, 400) + 2;
            rig.DropXp(new DVec2(.3, 0), raw); rig.Advance(.02);
            var earned = rig.Run.GrowthSettings.CostForLevels(1, rig.Player.Level - 1) + rig.Player.Growth.ExperienceIntoLevel;
            Assert.That(earned, Is.EqualTo(raw * 145 / 100));
            Assert.That(rig.Player.Growth.ExperienceRemainderHundredths, Is.EqualTo(90));
        }
        [Test]
        public void FractionsAccumulateAcrossOrbsAndLevelUpsWithoutRounding()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            new GrowthService(rig.Run).UpgradeShared(SharedUpgradeKind.Experience);
            for (int i = 0; i < 20; i++) { rig.DropXp(new DVec2(.3, 0), 1); rig.Advance(.02); }
            var earned = rig.Run.GrowthSettings.CostForLevels(1, rig.Player.Level - 1) + rig.Player.Growth.ExperienceIntoLevel;
            Assert.That(earned, Is.EqualTo(new BigInteger(23)));
        }
        [Test]
        public void ChangingBonusKeepsEarlierFractionsAndIntegerGrantsKeepTheRemainder()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            var upgrades = new GrowthService(rig.Run); upgrades.UpgradeShared(SharedUpgradeKind.Experience);
            rig.DropXp(new DVec2(.3, 0), 4); rig.Advance(.02); // 4.6
            upgrades.UpgradeShared(SharedUpgradeKind.Experience);
            rig.DropXp(new DVec2(.3, 0), 1); rig.Advance(.02); // 5.9 -> level 2, .9 left
            rig.GrantExperience(1);
            var view = new ExperienceHud(); new HudPresenter(view).Refresh(rig.Run);
            Assert.That(rig.Player.Level, Is.EqualTo(new BigInteger(2)));
            Assert.That(view.Experience, Is.EqualTo("경험치 1.9 / 8"));
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
            rig.DropXp(new DVec2(.3, 0), 1); rig.Advance(.02);
            Assert.That(rig.Player.Growth.ExperienceRemainderHundredths, Is.EqualTo(30));
            rig.World.AddItem(PowerupKind.MoveSpeed, rig.Player.Position); rig.Advance(.02);
            Assert.That(rig.Player.MoveSpeed, Is.EqualTo(5.4).Within(1e-8));
            var previous = rig.Player; rig.Hit(previous.Id, 1000); rig.Advance(.02);
            Assert.That(previous.IsAlive, Is.False);
            foreach (SharedUpgradeKind kind in Enum.GetValues(typeof(SharedUpgradeKind)))
                Assert.That(previous.Upgrades.GetLevel(kind), Is.EqualTo(BigInteger.Zero));
            Assert.That(previous.Growth.ExperienceRemainderHundredths, Is.Zero);
            rig.RestartAsync().GetAwaiter().GetResult();
            Assert.That(rig.Player.MoveSpeed, Is.EqualTo(3));
            Assert.That(rig.Player.Upgrades.HealingPerSecond, Is.Zero);
            Assert.That(rig.Player.Upgrades.PickupMultiplier, Is.EqualTo(1));
            Assert.That(rig.Player.Upgrades.ExperienceHundredthsFor(2), Is.EqualTo(new BigInteger(200)));
            Assert.That(rig.Player.Growth.ExperienceRemainderHundredths, Is.Zero);
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
        private sealed class ExperienceHud : IHudView
        {
            public string Experience; public double Fraction;
            public void Show(bool visible, string health, double healthFraction, string level, string experience,
                double experienceFraction, string survival, string kills, string weapons)
            { Experience = experience; Fraction = experienceFraction; }
        }
    }
}
