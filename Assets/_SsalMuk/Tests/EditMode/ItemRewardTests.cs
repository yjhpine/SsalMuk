using System;
using System.Linq;
using NUnit.Framework;
using SsalMuk.Core;
namespace SsalMuk.Tests.ItemsIntegration
{
    public sealed class ItemRewardTests
    {
        [Test]
        public void ChestCreatesOneFrozenDistinctOfferAndOnlyOneSelectedItem()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            Assert.That(rig.Run.ItemRewards.AddChest(1, ItemRarity.Rare), Is.True);
            Assert.That(rig.Run.ItemRewards.AddChest(1, ItemRarity.Rare), Is.False);
            var offer = rig.Run.ItemRewards.CurrentOffer;
            Assert.That(offer.Choices.Select(d => d.Id).Distinct().Count(), Is.EqualTo(3));
            Assert.That(offer.Choices.All(d => d.Rarity == ItemRarity.Rare), Is.True);
            rig.Advance(6); Assert.That(rig.Run.ItemRewards.CurrentOffer, Is.SameAs(offer));
            Assert.That(rig.Run.ItemRewards.TryQueueChoice(Guid.NewGuid(), offer.Id, 0), Is.False);
            Assert.That(rig.Run.ItemRewards.TryQueueChoice(rig.Run.Id, offer.Id, 0), Is.True);
            Assert.That(rig.Run.ItemRewards.TryQueueChoice(rig.Run.Id, offer.Id, 1), Is.False);
            rig.Advance(.02);
            Assert.That(rig.Run.Inventory.Items.Single().Definition, Is.SameAs(offer.Choices[0]));
            Assert.That(rig.Player.Level.ToString(), Is.EqualTo("1"));
            Assert.That(rig.Run.ItemRewards.CurrentOffer, Is.Null);
        }
        [Test]
        public void VisibleLevelStaysUntilChosenThenChestsAreFifoBeforeNextLevel()
        {
            using var rig = RunTestRig.Create(enableAi: false); rig.GrantExperience(20); rig.Advance(.02);
            var level = rig.CurrentOffer;
            rig.Run.ItemRewards.AddChest(10, ItemRarity.Common); rig.Run.ItemRewards.AddChest(20, ItemRarity.Uncommon);
            Assert.That(rig.CurrentOffer, Is.SameAs(level)); Assert.That(rig.Run.ItemRewards.CurrentOffer, Is.Null);
            rig.Choose(level.Id, 0); rig.Advance(.02);
            Assert.That(rig.CurrentOffer, Is.Null); var first = rig.Run.ItemRewards.CurrentOffer;
            Assert.That(first.ChestId, Is.EqualTo(10));
            rig.Run.ItemRewards.TryQueueChoice(rig.Run.Id, first.Id, 1); rig.Advance(.02);
            var second = rig.Run.ItemRewards.CurrentOffer; Assert.That(second.ChestId, Is.EqualTo(20));
            rig.Run.ItemRewards.TryQueueChoice(rig.Run.Id, second.Id, 2); rig.Advance(.02);
            Assert.That(rig.CurrentOffer, Is.Not.Null); Assert.That(rig.Run.ItemRewards.PendingCount, Is.Zero);
        }
        [Test]
        public void RewardBeforeFusionMakesOldInventoryCommandStaleAndPauseDefersAward()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            for (int i = 0; i < 3; i++) rig.Run.Inventory.TryAdd("c_blade");
            var ids = rig.Run.Inventory.Items.Select(i => i.Id).ToArray();
            rig.Run.Fusion.TryQueue(new FusionCommand(rig.Run.Id, rig.Run.Inventory.Version, ids, FusionMode.Random));
            rig.Run.ItemRewards.AddChest(1, ItemRarity.Common); var offer = rig.Run.ItemRewards.CurrentOffer;
            rig.Coordinator.Pause(); Assert.That(rig.Run.ItemRewards.TryQueueChoice(rig.Run.Id, offer.Id, 0), Is.False);
            rig.Coordinator.Resume(); rig.Run.ItemRewards.TryQueueChoice(rig.Run.Id, offer.Id, 0);
            rig.Coordinator.Pause(); rig.Advance(.02); Assert.That(rig.Run.Inventory.Items.Count, Is.EqualTo(3));
            rig.Coordinator.Resume(); rig.Advance(.02); Assert.That(rig.Run.Inventory.Items.Count, Is.EqualTo(4));
            Assert.That(ids.All(id => rig.Run.Inventory.TryGet(id, out _)), Is.True);
        }
        [Test]
        public void ChestOfferRandomnessDoesNotChangeLevelChoicesOrMapPositions()
        {
            using var a = RunTestRig.Create(seed: 77, enableAi: false); using var b = RunTestRig.Create(seed: 77, enableAi: false);
            a.Run.ItemRewards.AddChest(99, ItemRarity.Legendary); var offer = a.Run.ItemRewards.CurrentOffer;
            a.Run.ItemRewards.TryQueueChoice(a.Run.Id, offer.Id, 0); a.Advance(.02);
            a.GrantExperience(5); b.GrantExperience(5); a.Advance(.02); b.Advance(.02);
            CollectionAssert.AreEqual(a.CurrentOffer.Choices, b.CurrentOffer.Choices);
            a.Run.Chests.Enable(); b.Run.Chests.Enable();
            CollectionAssert.AreEqual(a.Run.Chests.Nearby(a.Player.Position).Select(c => c.Position), b.Run.Chests.Nearby(b.Player.Position).Select(c => c.Position));
        }
    }
    public sealed class ItemStatTests
    {
        [Test]
        public void ItemDamageAndAttackSpeedMultiplyCurrentWeaponGrowth()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            rig.Upgrade(WeaponKind.Sword, UpgradeKind.Damage, 2); rig.Upgrade(WeaponKind.Sword, UpgradeKind.Speed, 2);
            var before = rig.Run.CurrentWeaponStats(WeaponKind.Sword);
            rig.Run.Inventory.TryAdd("c_blade"); rig.Run.Inventory.TryAdd("u_blade"); rig.Run.Inventory.TryAdd("u_feather");
            var after = rig.Run.CurrentWeaponStats(WeaponKind.Sword);
            Assert.That(after.Damage, Is.EqualTo(before.Damage * 1.23).Within(1e-8));
            Assert.That(after.PeriodSeconds, Is.EqualTo(before.PeriodSeconds / 1.15).Within(1e-8));
        }
        [Test]
        public void ExperienceBonusesAddAndRetainExactHundredths()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            new GrowthService(rig.Run).UpgradeShared(SharedUpgradeKind.Experience);
            rig.Run.Inventory.TryAdd("c_book"); rig.DropXp(new DVec2(.3, 0), 1); rig.Advance(.02);
            Assert.That(rig.Player.Growth.ExperienceHundredthsIntoLevel.ToString(), Is.EqualTo("115"));
        }
        [Test]
        public void MoveHealingAndPickupAffectActualSimulation()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            var growth = new GrowthService(rig.Run); growth.UpgradeShared(SharedUpgradeKind.MoveSpeed);
            rig.Run.Inventory.TryAdd("u_boots");
            Assert.That(rig.Player.MoveSpeed, Is.EqualTo(3.63).Within(1e-8));
            rig.Hit(rig.Player.Id, 10); double hp = rig.Player.Health;
            rig.Run.Inventory.TryAdd("u_herb"); rig.Run.Inventory.TryAdd("u_magnet");
            long orb = rig.DropXp(new DVec2(rig.Run.PickupSettings.AttractionRadius * 1.3, 0), 1);
            rig.Advance(1);
            Assert.That(rig.Player.Health, Is.EqualTo(hp + .35).Within(1e-8));
            Assert.That(rig.World.TryGetExperience(orb, out _), Is.False);
        }
    }
}
