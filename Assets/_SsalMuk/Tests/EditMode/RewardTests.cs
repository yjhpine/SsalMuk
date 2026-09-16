using System;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class RewardTests
    {
        [TestCase(1), TestCase(2), TestCase(4)]
        public void OffersContainThreeDistinctEligibleRewards(int count)
        {
            var owned = Enum.GetValues(typeof(WeaponKind)).Cast<WeaponKind>().Take(count).ToArray();
            for (int seed = 1; seed <= 100; seed++)
            {
                var offer = new OfferGenerator().Generate(owned, new SeededRandom(seed), RewardWeights.TestDefaults());
                Assert.That(offer.Count, Is.EqualTo(3)); Assert.That(offer.Distinct().Count(), Is.EqualTo(3));
                Assert.That(offer.All(reward => owned.Contains(reward.Weapon) == reward.IsUpgrade), Is.True);
            }
        }
        [Test]
        public void CategoryWeightIsOneToThreeAndAnExhaustedCategoryIsRemoved()
        {
            var generator = new OfferGenerator(); var owned = new[] { WeaponKind.Sword };
            var acquisition = generator.Generate(owned, new Tape(0.2499, 0), RewardWeights.TestDefaults());
            var upgrade = generator.Generate(owned, new Tape(0.25, 0), RewardWeights.TestDefaults());
            Assert.That(acquisition[0].IsUpgrade, Is.False); Assert.That(upgrade[0].IsUpgrade, Is.True);
            var exhausted = generator.Generate(new[] { WeaponKind.Sword, WeaponKind.Spear }, new Tape(0), RewardWeights.TestDefaults());
            Assert.That(exhausted.Count(x => !x.IsUpgrade), Is.EqualTo(2)); Assert.That(exhausted[2].IsUpgrade, Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() => new RewardWeights(upgrade: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RewardWeights(acquisition: double.NaN));
            Assert.Throws<ArgumentException>(() => generator.Generate(Array.Empty<WeaponKind>(), new SeededRandom(1), RewardWeights.TestDefaults()));
        }
        [Test]
        public void ExtraLevelsKeepTheCurrentOfferAndChoicesApplyOnlyAtStepStart()
        {
            using var rig = RunTestRig.Create(enableAi: false); rig.GrantExperience(5); rig.Advance(0.02);
            Assert.That(rig.CurrentOffer, Is.Not.Null); var first = rig.CurrentOffer;
            rig.GrantExperience(100); rig.Advance(0.02);
            Assert.That(rig.CurrentOffer, Is.SameAs(first)); var pending = rig.Player.Growth.PendingChoices;
            var reward = first.Choices[0]; Assert.That(rig.Choose(first.Id, 0), Is.True);
            Assert.That(rig.Player.Growth.PendingChoices, Is.EqualTo(pending));
            rig.Choose(first.Id, 0); rig.Advance(0.02);
            Assert.That(rig.Player.Growth.PendingChoices, Is.EqualTo(pending - 1));
            if (reward.IsUpgrade) Assert.That(rig.Player.Weapons.Get(reward.Weapon).GetLevel(reward.UpgradeKind), Is.EqualTo(BigInteger.One));
            else Assert.That(rig.Player.Weapons.Owns(reward.Weapon), Is.True);
            Assert.That(rig.CurrentOffer.Id, Is.GreaterThan(first.Id));
            Assert.That(rig.Choose(first.Id, 0), Is.False);
        }
        [Test]
        public void InvalidSlotWrongRunAndOwnershipChangeCannotSpendAChoice()
        {
            using var rig = RunTestRig.Create(enableAi: false); rig.GrantExperience(5); rig.Advance(0.02);
            Assert.That(rig.CurrentOffer, Is.Not.Null); var offer = rig.CurrentOffer;
            Assert.That(rig.Run.Commands.TryQueueChoice(Guid.NewGuid(), offer.Id, 0), Is.False);
            Assert.That(rig.Choose(offer.Id, -1), Is.False); Assert.That(rig.Choose(offer.Id, 3), Is.False);
            Assert.That(rig.Choose(offer.Id, 0), Is.True); rig.Equip(WeaponKind.Spear); rig.Advance(0.02);
            Assert.That(rig.Player.Growth.PendingChoices, Is.EqualTo(BigInteger.One));
            Assert.That(rig.CurrentOffer.Id, Is.Not.EqualTo(offer.Id));
            Assert.That(rig.CurrentOffer.Choices, Has.No.Member(RewardId.Acquire(WeaponKind.Spear)));
        }
        [Test]
        public void AcquiringAllWeaponsUpdatesCandidatesAndStillAllowsUnlimitedUpgrades()
        {
            using var rig = RunTestRig.Create(seed: 23, enableAi: false); rig.GrantExperience(1000000); rig.Advance(0.02);
            for (int choice = 0; choice < 200; choice++)
            {
                Assert.That(rig.CurrentOffer, Is.Not.Null); var offer = rig.CurrentOffer;
                int slot = Enumerable.Range(0, 3).FirstOrDefault(index => !offer.Choices[index].IsUpgrade);
                Assert.That(rig.Choose(offer.Id, slot), Is.True); rig.Advance(0.02);
                Assert.That(rig.CurrentOffer.Choices.All(x => rig.Player.Weapons.Owns(x.Weapon) == x.IsUpgrade), Is.True);
            }
            Assert.That(rig.Player.Weapons.Kinds.Count, Is.EqualTo(4));
            Assert.That(rig.CurrentOffer.Choices.All(x => x.IsUpgrade), Is.True);
            Assert.That(rig.Player.Growth.PendingChoices > 0, Is.True);
        }
        [Test]
        public void DeathInvalidatesTheQueuedCommandAndAnOldRunCannotChooseForANewOne()
        {
            using var rig = RunTestRig.Create(enableAi: false); rig.GrantExperience(5); rig.Advance(0.02);
            Assert.That(rig.CurrentOffer, Is.Not.Null); var old = rig.Run; long id = rig.CurrentOffer.Id;
            Assert.That(rig.Choose(id, 0), Is.True); rig.Hit(rig.Player.Id, 1000); rig.Advance(0.02);
            Assert.That(old.CurrentOffer, Is.Null); Assert.That(old.Player.Growth.PendingChoices, Is.EqualTo(BigInteger.Zero));
            Assert.That(old.Commands.TryQueueChoice(old.Id, id, 0), Is.False);
            rig.RestartAsync().GetAwaiter().GetResult(); rig.GrantExperience(5); rig.Advance(0.02);
            Assert.That(rig.Run.Commands.TryQueueChoice(old.Id, rig.CurrentOffer.Id, 0), Is.False);
            Assert.That(rig.Player.Growth.PendingChoices, Is.EqualTo(BigInteger.One));
        }
        private sealed class Tape : IRandomSource
        {
            private readonly double[] values; private int index;
            public Tape(params double[] values) { this.values = values; }
            public double NextUnit() => values[index++ % values.Length];
        }
    }
}
