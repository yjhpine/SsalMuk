using System;
using System.Linq;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class ItemInventoryTests
    {
        private static long Add(RunModel run, string id)
        { Assert.That(run.Inventory.TryAdd(id), Is.True); return run.Inventory.Items.Last().Id; }
        private static FusionCommand Command(RunModel run, long[] ids, FusionMode mode = FusionMode.Random, string recipe = null)
            => new FusionCommand(run.Id, run.Inventory.Version, ids, mode, recipe);

        [Test]
        public void RandomFusionAcceptsThreeIdenticalDefinitionsAndAppendsOneResult()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            long survivor = Add(rig.Run, "c_boots");
            long[] ids = Enumerable.Range(0, 3).Select(_ => Add(rig.Run, "c_blade")).ToArray();
            long version = rig.Run.Inventory.Version;
            Assert.That(rig.Run.Fusion.TryQueue(Command(rig.Run, ids)), Is.True);
            Assert.That(rig.Run.Inventory.Items.Count, Is.EqualTo(4));
            rig.Run.Fusion.Step();
            Assert.That(rig.Run.Inventory.Items.Count, Is.EqualTo(2));
            Assert.That(rig.Run.Inventory.Items[0].Id, Is.EqualTo(survivor));
            Assert.That(rig.Run.Inventory.Items[1].Definition.Rarity, Is.EqualTo(ItemRarity.Uncommon));
            Assert.That(rig.Run.Inventory.Version, Is.EqualTo(version + 1));
        }

        [TestCase("c_blade,c_blade,c_feather", "u_blade")]
        [TestCase("c_feather,c_feather,c_boots", "u_feather")]
        [TestCase("c_boots,c_boots,c_book", "u_boots")]
        [TestCase("c_herb,c_herb,c_blade", "u_herb")]
        [TestCase("c_magnet,c_magnet,c_boots", "u_magnet")]
        [TestCase("c_book,c_book,c_herb", "u_book")]
        [TestCase("u_blade,u_herb,u_boots", "r_fang")]
        [TestCase("u_feather,u_magnet,u_book", "r_lightning")]
        [TestCase("u_blade,u_feather,u_magnet", "r_fire")]
        [TestCase("u_herb,u_boots,u_book", "r_shield")]
        [TestCase("r_fang,r_fang,r_fire", "l_fang")]
        [TestCase("r_lightning,r_lightning,r_fang", "l_lightning")]
        [TestCase("r_fire,r_fire,r_lightning", "l_fire")]
        [TestCase("r_shield,r_shield,r_fang", "l_shield")]
        [TestCase("l_fang,l_fang,l_fire", "x_fang")]
        [TestCase("l_lightning,l_lightning,l_fang", "x_lightning")]
        [TestCase("l_fire,l_fire,l_lightning", "x_fire")]
        [TestCase("l_shield,l_shield,l_fang", "x_shield")]
        public void FixedRecipesRespectCountsRegardlessOfSelectionOrder(string materials, string expected)
        {
            using var rig = RunTestRig.Create(enableAi: false);
            var ids = materials.Split(',').Select(id => Add(rig.Run, id)).Reverse().ToArray();
            var preview = rig.Run.Fusion.Preview(ids);
            Assert.That(preview.FixedResult.Id, Is.EqualTo(expected));
            Assert.That(rig.Run.Fusion.TryQueue(Command(rig.Run, ids, FusionMode.Fixed, preview.RecipeId)), Is.True);
            rig.Run.Fusion.Step();
            Assert.That(rig.Run.Inventory.Items.Single().Definition.Id, Is.EqualTo(expected));
        }

        [TestCase("c_blade,c_blade,u_blade", false)]
        [TestCase("x_fang,x_fang,x_fire", false)]
        [TestCase("c_blade,c_blade,c_feather", true)]
        public void InvalidMaterialsPreserveInventory(string materials, bool duplicateInstance)
        {
            using var rig = RunTestRig.Create(enableAi: false);
            var ids = materials.Split(',').Select(id => Add(rig.Run, id)).ToArray();
            if (duplicateInstance) ids[2] = ids[0];
            long version = rig.Run.Inventory.Version;
            Assert.That(rig.Run.Fusion.TryQueue(Command(rig.Run, ids)), Is.False);
            rig.Run.Fusion.Step();
            Assert.That(rig.Run.Inventory.Items.Count, Is.EqualTo(3));
            Assert.That(rig.Run.Inventory.Version, Is.EqualTo(version));
        }

        [Test]
        public void StaleVersionWrongRunMissingRecipeAndDuplicateCommandsNeverConsume()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            long[] ids = { Add(rig.Run, "c_blade"), Add(rig.Run, "c_blade"), Add(rig.Run, "c_feather") };
            var old = Command(rig.Run, ids);
            Assert.That(rig.Run.Fusion.TryQueue(new FusionCommand(Guid.NewGuid(), old.Version, ids, FusionMode.Random)), Is.False);
            Assert.That(rig.Run.Fusion.TryQueue(Command(rig.Run, ids, FusionMode.Fixed, "wrong")), Is.False);
            Assert.That(rig.Run.Fusion.TryQueue(old), Is.True);
            Assert.That(rig.Run.Fusion.TryQueue(old), Is.False);
            Add(rig.Run, "c_herb");
            rig.Run.Fusion.Step();
            Assert.That(rig.Run.Inventory.Items.Count, Is.EqualTo(4));
            Assert.That(rig.Run.Fusion.TryQueue(old), Is.False);
        }

        [Test]
        public void FailedFusionDoesNotAdvanceRandomAndSuccessRemovesMaterialBonuses()
        {
            using var a = RunTestRig.Create(seed: 42, enableAi: false);
            using var b = RunTestRig.Create(seed: 42, enableAi: false);
            long[] first = { Add(a.Run, "c_blade"), Add(a.Run, "c_blade"), Add(a.Run, "c_feather") };
            long[] second = { Add(b.Run, "c_blade"), Add(b.Run, "c_blade"), Add(b.Run, "c_feather") };
            Assert.That(a.Run.Inventory.Bonuses.Damage, Is.EqualTo(.1).Within(1e-9));
            Assert.That(a.Run.Fusion.TryQueue(Command(a.Run, new[] { first[0], first[0], first[1] })), Is.False);
            a.Run.Fusion.TryQueue(Command(a.Run, first)); b.Run.Fusion.TryQueue(Command(b.Run, second));
            a.Run.Fusion.Step(); b.Run.Fusion.Step();
            Assert.That(a.Run.Inventory.Items.Single().Definition.Id, Is.EqualTo(b.Run.Inventory.Items.Single().Definition.Id));
            Assert.That(a.Run.Inventory.Bonuses.Damage, Is.EqualTo(a.Run.Inventory.Items.Single().Definition.Bonuses.Damage));
        }

        [Test]
        public void PausedFusionWaitsAndDeathClearsOwnedEffects()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            long[] ids = { Add(rig.Run, "c_blade"), Add(rig.Run, "c_blade"), Add(rig.Run, "c_blade") };
            rig.Coordinator.Pause(); Assert.That(rig.Run.Fusion.TryQueue(Command(rig.Run, ids)), Is.False);
            rig.Coordinator.Resume(); Assert.That(rig.Run.Fusion.TryQueue(Command(rig.Run, ids)), Is.True);
            rig.Coordinator.Pause(); rig.Run.Fusion.Step(); Assert.That(rig.Run.Inventory.Items.Count, Is.EqualTo(3));
            rig.Coordinator.Resume(); rig.Hit(rig.Player.Id, 1000); rig.Advance(.02);
            rig.Run.Fusion.Step(); Assert.That(rig.Run.Inventory.Items.Count, Is.Zero);
        }
    }
}
