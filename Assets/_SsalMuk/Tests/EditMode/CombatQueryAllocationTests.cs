using System;
using System.Collections.Generic;
using NUnit.Framework;
using SsalMuk.Core;
using Unity.Profiling;

namespace SsalMuk.Tests
{
    public sealed class CombatQueryAllocationTests
    {
        [Test]
        public void WarmVisualLookupsDoNotAllocatePerUnitOrAttack()
        {
            var catalog = UnityEngine.Resources.Load<SsalMuk.Unity.GameCatalog>("Bootstrap/GameCatalog");
            Action lookup = () => {
                for (int i = 0; i < 4; i++)
                { catalog.Visuals.Unit((UnitKind)i); catalog.Visuals.Weapon((WeaponKind)i); }
            };
            lookup(); lookup();
            Assert.That(Allocations(lookup), Is.Zero, "Every visible unit and attack performs these lookups each frame.");
        }

        [Test]
        public void WarmCircleQueryDoesNotAllocatePerOccupiedCell()
        {
            var index = new SpatialIndex();
            var origin = WorldPosition.FromLocal(new DVec2(16, 16));
            for (int i = 0; i < 300; i++)
                index.Upsert(i + 1, origin.Offset(new DVec2(i % 20 - 10, i / 20 - 7)));
            var buffer = new List<long>(512);
            Action query = () => index.QueryCircle(origin, 18, buffer);
            query(); query();
            Assert.That(Allocations(query), Is.Zero, "A caller-owned buffer must avoid per-cell traversal allocations.");
            Assert.That(buffer.Count, Is.EqualTo(300));
            Assert.That(buffer, Is.Ordered);
        }

        [TestCase(WeaponKind.Sword)]
        [TestCase(WeaponKind.Spear)]
        [TestCase(WeaponKind.Axe)]
        public void RepeatedMeleeSweepReusesCandidatesWithoutTemporaryCollections(WeaponKind kind)
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            for (int i = 0; i < 40; i++) rig.Spawn(UnitKind.Normal, new DVec2(17.8 + i % 10 * .05, (i / 10 - 2) * .1), 10000);
            var definition = rig.Run.Definitions.GetWeapon(kind);
            var instance = new AttackInstance(new HitKey(rig.Run.Id, rig.Run.AllocateAttackId(), 0, 0), kind,
                0, definition.ActiveSeconds, new DVec2(1, 0), new WeaponStats(8, 18, 1), rig.Player.Position);
            var sword = new SwordAttack(rig.Run, rig.Movement, rig.Damage);
            var spear = new SpearAttack(rig.Run, rig.Movement, rig.Damage);
            var axe = new AxeAttack(rig.Run, rig.Movement, rig.Damage);
            Action sweep = () => {
                if (kind == WeaponKind.Sword) sword.Step(instance, 0, 1, 0, 1);
                else if (kind == WeaponKind.Spear) spear.Step(instance, 0, 1, 0, 1);
                else axe.Step(instance, 0, 1, 0, 1);
            };
            sweep(); sweep();
            int accepted = rig.Damage.CachedHitCount;
            Assert.That(accepted, Is.GreaterThan(0));
            Assert.That(Allocations(sweep), Is.Zero, "Repeated active-attack slices must reuse their candidate storage.");
            Assert.That(rig.Damage.CachedHitCount, Is.EqualTo(accepted));
        }

        private static long Allocations(Action operation)
        {
            // Unity's Mono returns zero for GC.GetAllocatedBytesForCurrentThread on this Editor.
            // The native GC.Alloc marker counts allocation events; it does not report byte size here.
            const ProfilerRecorderOptions options = ProfilerRecorderOptions.CollectOnlyOnCurrentThread;
            using var recorder = new ProfilerRecorder(ProfilerCategory.Memory, "GC.Alloc", 10000, options);
            Assert.That(recorder.Valid, Is.True);
            recorder.Start(); var calibration = new byte[1024]; recorder.Stop();
            Assert.That(recorder.Count, Is.GreaterThan(0), "Allocation recorder must observe the calibration allocation.");
            GC.KeepAlive(calibration);
            recorder.Reset(); recorder.Start(); operation(); recorder.Stop();
            Assert.That(recorder.WrappedAround, Is.False, "Allocation sample buffer overflowed.");
            long count = 0;
            for (int i = 0; i < recorder.Count; i++) count += recorder.GetSample(i).Count;
            return count;
        }
    }
}
