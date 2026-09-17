using System;
using System.Collections;
using NUnit.Framework;
using SsalMuk.Core;
using SsalMuk.Unity;
using UnityEngine;
using UnityEngine.TestTools;

namespace SsalMuk.Tests
{
    public sealed class FeedbackAndPoolTests
    {
        [UnityTest]
        public IEnumerator AcceptedHitsFlashOnceAndWalkingUsesOnlyMovementInput()
        {
            var catalog = Resources.Load<GameCatalog>("Bootstrap/GameCatalog");
            var root = new GameObject("HitFixture");
            try
            {
                using var rig = RunTestRig.Create(enableAi: false);
                using var pool = new ViewPool<UnitView>(rig.Run.Id, () => UnityEngine.Object.Instantiate(catalog.UnitViewPrefab, root.transform).GetComponent<UnitView>());
                var lease = pool.Rent(rig.Player.Id); Assert.That(pool.TryGet(lease, out var view), Is.True);
                view.Show(rig.Player, DVec2.Zero, catalog, 0, 0);
                Assert.That(view.Body.color, Is.EqualTo(Color.white));
                Assert.That(rig.Hit(rig.Player.Id, 1), Is.True); Assert.That(rig.Hit(rig.Player.Id, 1), Is.False);
                view.Show(rig.Player, DVec2.Zero, catalog, 0, 0.02);
                Assert.That(view.Body.color, Is.EqualTo(Color.red)); Assert.That(view.HitScaleRoot.localScale.x, Is.EqualTo(1.15f).Within(1e-5));
                Assert.That(view.WalkPivot.localRotation, Is.EqualTo(Quaternion.identity), "A hit alone cannot start walking.");
                Assert.That(rig.Player.HitSequence, Is.EqualTo(1)); Assert.That(rig.Player.BodyRadius, Is.EqualTo(0.28));
                view.Show(rig.Player, DVec2.Zero, catalog, 0.16, 0.14);
                Assert.That(view.Body.color, Is.EqualTo(Color.white)); Assert.That(view.HitScaleRoot.localScale, Is.EqualTo(Vector3.one));
                rig.Movement.SetMoveIntent(rig.Player.Id, new DVec2(1, 0)); rig.Movement.Step(0.02);
                view.Show(rig.Player, DVec2.Zero, catalog, 0.26, 0.1);
                Assert.That(Quaternion.Angle(view.WalkPivot.localRotation, Quaternion.identity), Is.GreaterThan(1));
                Assert.That(view.WalkPivot.localPosition.y, Is.GreaterThan(-0.28f));
                rig.Movement.SetMoveIntent(rig.Player.Id, DVec2.Zero); rig.Movement.Step(0.02);
                rig.Spawn(UnitKind.Normal, new DVec2(1, 1), 1000); rig.Advance(0.02);
                Assert.That(rig.Simulation.Sword.ActiveAttacks.Count, Is.GreaterThan(0));
                view.Show(rig.Player, DVec2.Zero, catalog, 1, 0.74);
                Assert.That(view.WalkPivot.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(view.WalkPivot.localPosition.y, Is.EqualTo(-0.28f).Within(1e-5));
                pool.Return(lease); var next = pool.Rent(900); Assert.That(pool.TryGet(next, out var reused), Is.True);
                Assert.That(reused.TryApplyHit(lease, 999, 1), Is.False); Assert.That(reused.Body.color, Is.EqualTo(Color.white));
                yield return null;
            }
            finally { UnityEngine.Object.Destroy(root); }
        }
        [UnityTest]
        public IEnumerator ReturnedLeaseCannotAffectTheNextEntityAndPoolDisposalReclaimsObjects()
        {
            var root = new GameObject("PoolFixture");
            try
            {
                using var pool = new ViewPool<PoolProbeView>(Guid.NewGuid(), () =>
                { var go = new GameObject("Pooled"); go.transform.SetParent(root.transform); return go.AddComponent<PoolProbeView>(); });
                var first = pool.Rent(7); Assert.That(pool.TryGet(first, out var oldView), Is.True);
                oldView.transform.localScale = Vector3.one * 2;
                Assert.That(pool.Return(first), Is.True); Assert.That(pool.Return(first), Is.False);
                var second = pool.Rent(8); Assert.That(pool.TryGet(second, out var current), Is.True);
                Assert.That(current, Is.SameAs(oldView)); Assert.That(second.Generation, Is.GreaterThan(first.Generation));
                Assert.That(current.transform.localScale, Is.EqualTo(Vector3.one)); Assert.That(current.Accepts(first), Is.False);
                Assert.That(pool.TryGet(first, out _), Is.False); Assert.That(pool.Return(first), Is.False);
                Assert.That(pool.ActiveCount, Is.EqualTo(1)); Assert.That(current.Accepts(second), Is.True);
                pool.Dispose(); yield return null; Assert.That(root.transform.childCount, Is.Zero);
            }
            finally { UnityEngine.Object.Destroy(root); }
        }
        [UnityTest]
        public IEnumerator UnitBodyHasIndependentWalkAndHitPivots()
        {
            var catalog = Resources.Load<GameCatalog>("Bootstrap/GameCatalog");
            var go = UnityEngine.Object.Instantiate(catalog.UnitViewPrefab);
            try
            {
                using var rig = RunTestRig.Create(enableAi: false); var view = go.GetComponent<UnitView>();
                view.Show(rig.Player, DVec2.Zero, catalog);
                var sprite = go.transform.Find("WalkPivot/HitScaleRoot/Sprite");
                Assert.That(sprite, Is.Not.Null, "The foot pivot and hit scale must be separate from the logical root.");
                Assert.That(go.transform.Find("Shadow"), Is.Null, "The prefab must not retain its old ground shadow.");
                Assert.That(go.transform.Find("GroundShadow"), Is.Null, "Showing a unit must not recreate the removed shadow.");
                Assert.That(rig.Player.BodyRadius, Is.EqualTo(0.28));
                yield return null;
            }
            finally { UnityEngine.Object.Destroy(go); }
        }
    }
    public sealed class PoolProbeView : PooledView { }
}
