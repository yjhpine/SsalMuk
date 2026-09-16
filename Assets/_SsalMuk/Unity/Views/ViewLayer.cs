using System;
using System.Collections.Generic;
namespace SsalMuk.Unity
{
    internal sealed class ViewLayer<T> : IDisposable where T : PooledView
    {
        private readonly ViewPool<T> pool;
        private readonly Dictionary<long, LeaseToken> leases = new Dictionary<long, LeaseToken>();
        private readonly HashSet<long> seen = new HashSet<long>();
        private readonly List<long> returns = new List<long>();
        public int ActiveCount => pool.ActiveCount;
        public int RetainedCount => pool.RetainedCount;
        public IEnumerable<T> ActiveViews { get { foreach (var token in leases.Values) if (pool.TryGet(token, out var view)) yield return view; } }
        public ViewLayer(Guid runId, Func<T> factory) { pool = new ViewPool<T>(runId, factory); }
        public void BeginFrame() => seen.Clear();
        public T Show(long id)
        {
            seen.Add(id);
            if (!leases.TryGetValue(id, out var token)) { token = pool.Rent(id); leases.Add(id, token); }
            if (!pool.TryGet(token, out var view)) throw new InvalidOperationException("The visible lease is no longer current.");
            return view;
        }
        public void EndFrame()
        {
            returns.Clear(); foreach (long id in leases.Keys) if (!seen.Contains(id)) returns.Add(id);
            foreach (long id in returns) { pool.Return(leases[id]); leases.Remove(id); }
        }
        public void Dispose() { pool.Dispose(); leases.Clear(); seen.Clear(); }
    }
}
