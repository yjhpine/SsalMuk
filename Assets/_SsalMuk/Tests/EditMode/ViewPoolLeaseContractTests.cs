using System;
using NUnit.Framework;
using SsalMuk.Unity;
using UnityEngine;

namespace SsalMuk.Tests
{
    public sealed class ViewPoolLeaseContractTests
    {
        private GameObject root;
        private int createdViews;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("ViewPoolLeaseContractFixture");
            createdViews = 0;
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void RepeatedRentOfAnActiveEntityKeepsTheLeaseAndView()
        {
            using var pool = new ViewPool<LeaseContractProbeView>(Guid.NewGuid(), CreateView);
            var first = pool.Rent(7);
            Assert.That(pool.TryGet(first, out var original), Is.True);
            original.transform.localPosition = new Vector3(3, 4, 0);

            var repeated = pool.Rent(7);

            Assert.That(repeated, Is.EqualTo(first));
            Assert.That(pool.TryGet(repeated, out var current), Is.True);
            Assert.That(current, Is.SameAs(original));
            Assert.That(current.transform.localPosition, Is.EqualTo(new Vector3(3, 4, 0)));
            Assert.That(createdViews, Is.EqualTo(1));
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            Assert.That(pool.RetainedCount, Is.EqualTo(1));
        }

        [Test]
        public void ReRentingTheSameEntityRejectsItsPreviousGenerationWithoutReturningTheCurrentView()
        {
            using var pool = new ViewPool<LeaseContractProbeView>(Guid.NewGuid(), CreateView);
            var previous = pool.Rent(7);
            Assert.That(pool.TryGet(previous, out var original), Is.True);
            Assert.That(pool.Return(previous), Is.True);

            var current = pool.Rent(7);
            Assert.That(pool.TryGet(current, out var reused), Is.True);
            Assert.That(reused, Is.SameAs(original));
            Assert.That(current.RunId, Is.EqualTo(previous.RunId));
            Assert.That(current.EntityId, Is.EqualTo(previous.EntityId));
            Assert.That(current.Generation, Is.GreaterThan(previous.Generation));

            Assert.That(reused.Accepts(previous), Is.False);
            Assert.That(pool.TryGet(previous, out var staleView), Is.False);
            Assert.That(staleView, Is.Null);
            Assert.That(pool.Return(previous), Is.False);
            Assert.That(pool.TryGet(current, out var stillCurrent), Is.True);
            Assert.That(stillCurrent, Is.SameAs(reused));
            Assert.That(reused.Accepts(current), Is.True);
            Assert.That(reused.gameObject.activeSelf, Is.True);
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            Assert.That(pool.RetainedCount, Is.EqualTo(1));
        }

        [Test]
        public void MatchingEntityAndGenerationFromAnotherRunCannotAccessOrReturnEitherView()
        {
            using var firstPool = new ViewPool<LeaseContractProbeView>(Guid.NewGuid(), CreateView);
            using var secondPool = new ViewPool<LeaseContractProbeView>(Guid.NewGuid(), CreateView);
            var first = firstPool.Rent(7);
            var second = secondPool.Rent(7);
            Assert.That(first.EntityId, Is.EqualTo(second.EntityId));
            Assert.That(first.Generation, Is.EqualTo(second.Generation));
            Assert.That(first.RunId, Is.Not.EqualTo(second.RunId));

            Assert.That(firstPool.TryGet(second, out _), Is.False);
            Assert.That(secondPool.TryGet(first, out _), Is.False);
            Assert.That(firstPool.Return(second), Is.False);
            Assert.That(secondPool.Return(first), Is.False);

            Assert.That(firstPool.TryGet(first, out var firstView), Is.True);
            Assert.That(secondPool.TryGet(second, out var secondView), Is.True);
            Assert.That(firstView.Accepts(second), Is.False);
            Assert.That(secondView.Accepts(first), Is.False);
            Assert.That(firstView.gameObject.activeSelf, Is.True);
            Assert.That(secondView.gameObject.activeSelf, Is.True);
            Assert.That(firstPool.ActiveCount, Is.EqualTo(1));
            Assert.That(secondPool.ActiveCount, Is.EqualTo(1));
        }

        [Test]
        public void DisposalReclaimsActiveAndReturnedViewsAndRejectsFurtherLeases()
        {
            using var pool = new ViewPool<LeaseContractProbeView>(Guid.NewGuid(), CreateView);
            var active = pool.Rent(7);
            var returned = pool.Rent(8);
            Assert.That(pool.TryGet(active, out var activeView), Is.True);
            Assert.That(pool.TryGet(returned, out var returnedView), Is.True);
            var activeObject = activeView.gameObject;
            var returnedObject = returnedView.gameObject;
            Assert.That(pool.Return(returned), Is.True);
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            Assert.That(pool.RetainedCount, Is.EqualTo(2));

            pool.Dispose();

            Assert.That(activeObject == null, Is.True);
            Assert.That(returnedObject == null, Is.True);
            Assert.That(root.transform.childCount, Is.Zero);
            Assert.That(pool.ActiveCount, Is.Zero);
            Assert.That(pool.RetainedCount, Is.Zero);
            Assert.That(pool.TryGet(active, out _), Is.False);
            Assert.That(pool.Return(active), Is.False);
            Assert.That(pool.TryGet(returned, out _), Is.False);
            Assert.That(pool.Return(returned), Is.False);
            Assert.Throws<ObjectDisposedException>(() => pool.Rent(7));
            Assert.DoesNotThrow(pool.Dispose);
        }

        private LeaseContractProbeView CreateView()
        {
            createdViews++;
            var gameObject = new GameObject("LeaseContractProbe");
            gameObject.transform.SetParent(root.transform, false);
            return gameObject.AddComponent<LeaseContractProbeView>();
        }
    }

    public sealed class LeaseContractProbeView : PooledView { }
}
