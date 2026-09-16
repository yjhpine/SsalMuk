using System;
using System.Collections.Generic;
using UnityEngine;
namespace SsalMuk.Unity
{
    public sealed class ViewPool<T> : IDisposable where T : PooledView
    {
        private readonly Guid runId;
        private readonly Func<T> factory;
        private readonly Dictionary<long, T> active = new Dictionary<long, T>();
        private readonly Stack<T> free = new Stack<T>();
        private long generation;
        private bool disposed;
        public int ActiveCount => active.Count;
        public int RetainedCount => active.Count + free.Count;
        public ViewPool(Guid runId, Func<T> factory)
        {
            if (runId == Guid.Empty) throw new ArgumentException("A pool belongs to a run.");
            this.runId = runId; this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }
        public LeaseToken Rent(long entityId)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ViewPool<T>));
            if (entityId <= 0) throw new ArgumentOutOfRangeException(nameof(entityId));
            if (active.TryGetValue(entityId, out var existing)) return existing.Lease;
            var view = free.Count > 0 ? free.Pop() : factory();
            if (view == null) throw new InvalidOperationException("A view factory returned no object.");
            view.ResetVisuals(); var token = new LeaseToken(runId, entityId, checked(++generation));
            view.BindLease(token); view.gameObject.SetActive(true); active.Add(entityId, view); return token;
        }
        public bool TryGet(LeaseToken token, out T view)
        {
            view = null;
            if (disposed || token.RunId != runId || !active.TryGetValue(token.EntityId, out var candidate) || !candidate.Accepts(token)) return false;
            view = candidate; return true;
        }
        public bool Return(LeaseToken token)
        {
            if (!TryGet(token, out var view)) return false;
            active.Remove(token.EntityId); view.ResetVisuals(); view.gameObject.SetActive(false); free.Push(view); return true;
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            foreach (var view in active.Values) Reclaim(view);
            foreach (var view in free) Reclaim(view);
            active.Clear(); free.Clear();
        }
        private static void Reclaim(T view)
        {
            if (view == null) return; view.ResetVisuals(); view.gameObject.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(view.gameObject); else UnityEngine.Object.DestroyImmediate(view.gameObject);
        }
    }
}
