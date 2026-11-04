using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SsalMuk.Core;
namespace SsalMuk.Tests
{
    public sealed class ItemEffectTests
    {
        [Test]
        public void LifestealUsesActualHealthDamageAndSharedCooldownSurvivesExtraCopy()
        {
            using var rig = RunTestRig.Create(enableAi: false); rig.Hit(rig.Player.Id, 50);
            rig.Run.Inventory.TryAdd("r_fang");
            long first = rig.Spawn(UnitKind.Normal, new DVec2(10, 0), 3);
            double actual = 0; rig.Damage.Accepted += hit => { if (hit.Request.TargetId == first) actual = hit.ActualHealthDamage; };
            rig.Hit(first, 100); rig.Simulation.ItemEffects.ProcessHits();
            Assert.That(actual, Is.EqualTo(3)); Assert.That(rig.Player.Health, Is.EqualTo(50.3).Within(1e-8));
            rig.Run.Inventory.TryAdd("r_fang");
            long second = rig.Spawn(UnitKind.Normal, new DVec2(20, 0), 1000);
            rig.Hit(second, 100); rig.Simulation.ItemEffects.ProcessHits();
            Assert.That(rig.Player.Health, Is.EqualTo(50.3).Within(1e-8));
            rig.Advance(.5); rig.Hit(second, 100); rig.Simulation.ItemEffects.ProcessHits();
            Assert.That(rig.Player.Health, Is.EqualTo(54.3).Within(1e-8), "Two caps sum to four, with one emitter.");
        }
        [Test]
        public void ChainHitsNearestDistinctEnemiesWithIdTieBreakAndNoExtraEmitterPerCopy()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            rig.Run.Inventory.TryAdd("r_lightning"); rig.Run.Inventory.TryAdd("r_lightning");
            long source = rig.Spawn(UnitKind.Normal, new DVec2(10, 0), 100);
            long a = rig.Spawn(UnitKind.Normal, new DVec2(12, 1), 100), b = rig.Spawn(UnitKind.Normal, new DVec2(12, -1), 100);
            long beyond = rig.Spawn(UnitKind.Normal, new DVec2(14, 1), 100);
            var hits = new List<long>(); rig.Damage.Accepted += hit => { if (hit.Request.Origin == DamageOrigin.Item) hits.Add(hit.Request.TargetId); };
            rig.Hit(source, 10); rig.Simulation.ItemEffects.ProcessHits();
            CollectionAssert.AreEqual(new[] { a, b }, hits);
            Assert.That(rig.Unit(a).Health, Is.EqualTo(96)); Assert.That(rig.Unit(b).Health, Is.EqualTo(96));
            Assert.That(rig.Unit(beyond).Health, Is.EqualTo(100));
            rig.Hit(source, 10); rig.Simulation.ItemEffects.ProcessHits(); Assert.That(hits.Count, Is.EqualTo(2));
            Assert.That(rig.Damage.CachedHitCount, Is.Zero, "Proc attacks retire their hit-cache keys.");
        }
        [Test]
        public void ExplosionKillsGiveRewardsButCannotTriggerAnotherExplosion()
        {
            using var rig = RunTestRig.Create(enableAi: false); rig.Run.Inventory.TryAdd("r_fire");
            long primary = rig.Spawn(UnitKind.Normal, new DVec2(10, 0), 1);
            long secondary = rig.Spawn(UnitKind.Normal, new DVec2(11, 0), 4);
            long outside = rig.Spawn(UnitKind.Normal, new DVec2(12.5, 0), 5);
            rig.Hit(primary, 10); rig.Simulation.ItemEffects.ProcessHits(); rig.Death.Flush();
            Assert.That(rig.Run.Kills, Is.EqualTo(2)); Assert.That(rig.Run.Experience.Count, Is.EqualTo(2));
            Assert.That(rig.Unit(outside).Health, Is.EqualTo(5));
            rig.Simulation.ItemEffects.ProcessHits(); rig.Death.Flush(); Assert.That(rig.Run.Kills, Is.EqualTo(2));
        }
        [Test]
        public void ItemOriginDamageDoesNotTriggerLifestealChainOrExplosion()
        {
            using var rig = RunTestRig.Create(enableAi: false); rig.Hit(rig.Player.Id, 50);
            foreach (string id in new[] { "r_fang", "r_fire", "r_lightning" }) rig.Run.Inventory.TryAdd(id);
            long target = rig.Spawn(UnitKind.Normal, new DVec2(10, 0), 1), other = rig.Spawn(UnitKind.Normal, new DVec2(11, 0), 100);
            long attack = rig.Run.AllocateAttackId();
            rig.Damage.TryApply(new DamageRequest(new HitKey(rig.Run.Id, attack, 0, 0), rig.Player.Id, target, 100, DVec2.Zero, origin: DamageOrigin.Item), rig.Clock.ElapsedSeconds);
            rig.Damage.CompleteAttack(attack); rig.Simulation.ItemEffects.ProcessHits(); rig.Death.Flush();
            Assert.That(rig.Player.Health, Is.EqualTo(50)); Assert.That(rig.Unit(other).Health, Is.EqualTo(100)); Assert.That(rig.Run.Kills, Is.EqualTo(1));
        }
        [Test]
        public void ShieldAbsorbsAfterInvulnerabilityAndRechargesOnActiveTime()
        {
            using var rig = RunTestRig.Create(enableAi: false); rig.Run.Inventory.TryAdd("r_shield");
            Assert.That(rig.Player.Shield, Is.EqualTo(20));
            long sequence = rig.Player.HitSequence; Assert.That(rig.Hit(rig.Player.Id, 15), Is.True);
            Assert.That(rig.Player.Shield, Is.EqualTo(5)); Assert.That(rig.Player.Health, Is.EqualTo(100));
            Assert.That(rig.Player.HitSequence, Is.EqualTo(sequence + 1)); Assert.That(rig.Player.InvulnerableUntil, Is.GreaterThan(0));
            Assert.That(rig.Hit(rig.Player.Id, 15), Is.False); Assert.That(rig.Player.Shield, Is.EqualTo(5));
            rig.Coordinator.Pause(); rig.Advance(10); Assert.That(rig.Player.Shield, Is.EqualTo(5)); rig.Coordinator.Resume();
            rig.Advance(.26); Assert.That(rig.Hit(rig.Player.Id, 10), Is.True);
            Assert.That(rig.Player.Shield, Is.Zero); Assert.That(rig.Player.Health, Is.EqualTo(95));
            rig.Advance(7.74); Assert.That(rig.Player.Shield, Is.EqualTo(20));
        }
        [Test]
        public void ShieldFusionPreservesRemainingChargeAndRemovingFamilyClearsIt()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            foreach (string id in new[] { "r_shield", "r_shield", "r_fang" }) rig.Run.Inventory.TryAdd(id);
            Assert.That(rig.Player.Shield, Is.EqualTo(20), "A second copy increases capacity without filling it.");
            rig.Hit(rig.Player.Id, 15);
            var ids = rig.Run.Inventory.Items.Select(i => i.Id).ToArray(); var preview = rig.Run.Fusion.Preview(ids);
            rig.Run.Fusion.TryQueue(new FusionCommand(rig.Run.Id, rig.Run.Inventory.Version, ids, FusionMode.Fixed, preview.RecipeId)); rig.Advance(.02);
            Assert.That(rig.Run.Inventory.Items.Single().Definition.Id, Is.EqualTo("l_shield")); Assert.That(rig.Player.Shield, Is.EqualTo(5));
            rig.Run.Inventory.Clear(); Assert.That(rig.Player.Shield, Is.Zero);
            rig.Run.Inventory.TryAdd("r_shield"); Assert.That(rig.Player.Shield, Is.EqualTo(20));
        }
    }
}
