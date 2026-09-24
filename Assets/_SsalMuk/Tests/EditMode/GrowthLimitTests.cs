using System;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class GrowthLimitTests
    {
        [TestCase(WeaponKind.Sword, 40, 8)] [TestCase(WeaponKind.Spear, 40, 8)]
        [TestCase(WeaponKind.Axe, 40, 8)] [TestCase(WeaponKind.Fireball, 30, 6)]
        public void EachUpgradeAddsOneAlternatingCopyUntilTheWeaponTotalCap(WeaponKind kind, int degrees, int maximum)
        {
            using var rig = RunTestRig.Create(enableAi: false);
            if (kind != WeaponKind.Sword) rig.Equip(kind);
            rig.Spawn(UnitKind.Normal, new DVec2(10, 0), 1000);
            var multiples = new[] { 0, 1, -1, 2, -2, 3, -3, 4 };
            for (int level = 0; level < maximum; level++)
            {
                if (level > 0) rig.Upgrade(kind, UpgradeKind.Copies);
                var definition = rig.Run.Definitions.GetWeapon(kind);
                var stats = StatCalculator.Calculate(definition, rig.Player.Weapons.Get(kind));
                Assert.That(stats.Copies, Is.EqualTo(new BigInteger(1 + level)));
                using var runtime = new WeaponRuntime(rig.Run, kind, rig.Movement, rig.Damage);
                int count = 0;
                runtime.Launched += attack =>
                {
                    Assert.That(Math.Atan2(attack.Direction.Y, attack.Direction.X) * 180 / Math.PI, Is.EqualTo(multiples[count++] * degrees).Within(1e-8));
                    Assert.That(attack.Origin, Is.EqualTo(rig.Player.Position));
                };
                runtime.Tick(0, .02);
                Assert.That(count, Is.EqualTo(level + 1));
            }
            var state = rig.Player.Weapons.Get(kind);
            Assert.That(state.CanUpgrade(UpgradeKind.Copies), Is.False);
            Assert.Throws<InvalidOperationException>(() => rig.Upgrade(kind, UpgradeKind.Copies));
            Assert.That(state.GetLevel(UpgradeKind.Copies), Is.EqualTo(new BigInteger(maximum - 1)));
        }

        [TestCase(WeaponKind.Sword, 8)] [TestCase(WeaponKind.Spear, 8)]
        [TestCase(WeaponKind.Axe, 8)] [TestCase(WeaponKind.Fireball, 6)]
        public void CrossingTheCopyCapInOneUpgradeDoesNotPartlyApplyIt(WeaponKind kind, int maximum)
        {
            using var rig = RunTestRig.Create(enableAi: false);
            if (kind != WeaponKind.Sword) rig.Equip(kind);
            rig.Upgrade(kind, UpgradeKind.Copies, maximum - 2);
            Assert.Throws<InvalidOperationException>(() => rig.Upgrade(kind, UpgradeKind.Copies, 2));
            var state = rig.Player.Weapons.Get(kind);
            Assert.That(state.GetLevel(UpgradeKind.Copies), Is.EqualTo(new BigInteger(maximum - 2)));
            Assert.That(state.CanUpgrade(UpgradeKind.Copies), Is.True);
            rig.Upgrade(kind, UpgradeKind.Copies);
            Assert.That(StatCalculator.Calculate(rig.Run.Definitions.GetWeapon(kind), state).Copies, Is.EqualTo(new BigInteger(maximum)));
        }

        [Test]
        public void MaxedCopiesDisappearFromFutureOffersWhileOtherUpgradesRemain()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            foreach (WeaponKind kind in Enum.GetValues(typeof(WeaponKind)))
            {
                if (kind != WeaponKind.Sword) rig.Equip(kind);
                rig.Upgrade(kind, UpgradeKind.Copies, kind == WeaponKind.Fireball ? 5 : 7);
            }
            rig.GrantExperience(1000000);
            for (int i = 0; i < 100; i++)
            {
                rig.Run.Rewards.RefreshOffer(); var offer = rig.CurrentOffer;
                Assert.That(offer.Choices.Count, Is.EqualTo(3));
                Assert.That(offer.Choices.Any(choice => choice.IsUpgrade && choice.UpgradeKind == UpgradeKind.Copies), Is.False);
                Assert.That(rig.Choose(offer.Id, 0), Is.True); rig.Run.Rewards.Step();
            }
        }

        [Test]
        public void AQueuedCopyChoiceBecomingCappedDoesNotConsumeTheChoiceOrThrow()
        {
            using var rig = RunTestRig.Create(enableAi: false); rig.GrantExperience(1000000);
            for (int i = 0; i < 100; i++)
            {
                rig.Run.Rewards.RefreshOffer(); var offer = rig.CurrentOffer;
                int slot = offer.Choices.ToList().FindIndex(choice => choice.IsUpgrade && choice.UpgradeKind == UpgradeKind.Copies);
                if (slot >= 0)
                {
                    var reward = offer.Choices[slot]; var pending = rig.Player.Growth.PendingChoices;
                    Assert.That(rig.Choose(offer.Id, slot), Is.True);
                    int maximumLevel = reward.Weapon == WeaponKind.Fireball ? 5 : 7;
                    rig.Upgrade(reward.Weapon, UpgradeKind.Copies, maximumLevel - rig.Player.Weapons.Get(reward.Weapon).GetLevel(UpgradeKind.Copies));
                    Assert.DoesNotThrow(() => rig.Run.Rewards.Step());
                    Assert.That(rig.Player.Growth.PendingChoices, Is.EqualTo(pending));
                    Assert.That(rig.CurrentOffer.Id, Is.Not.EqualTo(offer.Id));
                    Assert.That(rig.CurrentOffer.Choices, Has.No.Member(reward)); return;
                }
                rig.Choose(offer.Id, 0); rig.Run.Rewards.Step();
            }
            Assert.Fail("Seed did not offer copies.");
        }

        [TestCase(WeaponKind.Sword)] [TestCase(WeaponKind.Spear)]
        [TestCase(WeaponKind.Axe)] [TestCase(WeaponKind.Fireball)]
        public void RepeatLevelTwoIsThreeStrikesAndFurtherGrowthIsRejectedAtomically(WeaponKind kind)
        {
            using var rig = RunTestRig.Create(enableAi: false);
            if (kind != WeaponKind.Sword) rig.Equip(kind);
            rig.Upgrade(kind, UpgradeKind.Repeats, 2);
            var state = rig.Player.Weapons.Get(kind);
            Assert.That(StatCalculator.Calculate(rig.Run.Definitions.GetWeapon(kind), state).Repeats, Is.EqualTo(new BigInteger(3)));
            Assert.Throws<InvalidOperationException>(() => rig.Upgrade(kind, UpgradeKind.Repeats));
            Assert.That(state.GetLevel(UpgradeKind.Repeats), Is.EqualTo(new BigInteger(2)));
        }

        [Test]
        public void MaxedRepeatsDisappearFromEveryFutureOfferWithoutRemovingOtherUpgrades()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            foreach (WeaponKind kind in Enum.GetValues(typeof(WeaponKind)))
            {
                if (kind != WeaponKind.Sword) rig.Equip(kind);
                rig.Upgrade(kind, UpgradeKind.Repeats, 2);
            }
            rig.GrantExperience(1000000);
            for (int i = 0; i < 100; i++)
            {
                rig.Run.Rewards.RefreshOffer(); var offer = rig.CurrentOffer;
                Assert.That(offer.Choices.Count, Is.EqualTo(3));
                Assert.That(offer.Choices.All(choice => choice.IsUpgrade && choice.UpgradeKind != UpgradeKind.Repeats), Is.True);
                Assert.That(rig.Choose(offer.Id, 0), Is.True); rig.Run.Rewards.Step();
            }
        }

        [Test]
        public void AQueuedRepeatChoiceBecomingCappedDoesNotConsumeTheChoiceOrThrow()
        {
            using var rig = RunTestRig.Create(enableAi: false); rig.GrantExperience(1000000);
            for (int i = 0; i < 100; i++)
            {
                rig.Run.Rewards.RefreshOffer(); var offer = rig.CurrentOffer;
                int slot = offer.Choices.ToList().FindIndex(choice => choice.IsUpgrade && choice.UpgradeKind == UpgradeKind.Repeats);
                if (slot >= 0)
                {
                    var reward = offer.Choices[slot]; var pending = rig.Player.Growth.PendingChoices;
                    Assert.That(rig.Choose(offer.Id, slot), Is.True);
                    rig.Upgrade(reward.Weapon, UpgradeKind.Repeats, 2);
                    Assert.DoesNotThrow(() => rig.Run.Rewards.Step());
                    Assert.That(rig.Player.Growth.PendingChoices, Is.EqualTo(pending));
                    Assert.That(rig.CurrentOffer.Id, Is.Not.EqualTo(offer.Id)); return;
                }
                rig.Choose(offer.Id, 0); rig.Run.Rewards.Step();
            }
            Assert.Fail("Seed did not offer repeats.");
        }
    }
}
