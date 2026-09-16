using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    public sealed class DamageService : IDisposable
    {
        private readonly RunModel run;
        private readonly MovementSystem movement;
        private readonly double invulnerabilitySeconds;
        private readonly Dictionary<long, HashSet<HitKey>> acceptedKeys = new Dictionary<long, HashSet<HitKey>>();
        private readonly Dictionary<long, HashSet<long>> hitTargets = new Dictionary<long, HashSet<long>>();
        private readonly SortedList<long, long> completed = new SortedList<long, long>();
        private bool disposed;
        public event Action<CombatEvent> Accepted;
        public int CachedHitCount { get { int count = 0; foreach (var keys in acceptedKeys.Values) count += keys.Count; return count; } }
        public int CompletedRangeCount => completed.Count;
        public void CompleteAttack(long attackId)
        {
            if (disposed) return;
            if (attackId <= 0) throw new ArgumentOutOfRangeException(nameof(attackId));
            if (hitTargets.TryGetValue(attackId, out var targets))
            {
                foreach (long target in targets)
                    if (acceptedKeys.TryGetValue(target, out var keys))
                    { keys.RemoveWhere(key => key.AttackId == attackId); if (keys.Count == 0) acceptedKeys.Remove(target); }
                hitTargets.Remove(attackId);
            }
            int before = RangeBefore(attackId);
            if (before >= 0 && completed.Values[before] >= attackId) return;
            long start = attackId, end = attackId;
            if (before >= 0 && completed.Values[before] == attackId - 1)
            { start = completed.Keys[before]; completed.RemoveAt(before); before--; }
            int next = before + 1;
            if (next < completed.Count && attackId < long.MaxValue && completed.Keys[next] == attackId + 1)
            { end = completed.Values[next]; completed.RemoveAt(next); }
            completed.Add(start, end);
        }
        private int RangeBefore(long id)
        {
            int lo = 0, hi = completed.Count - 1, result = -1;
            while (lo <= hi) { int mid = lo + (hi - lo) / 2; if (completed.Keys[mid] <= id) { result = mid; lo = mid + 1; } else hi = mid - 1; }
            return result;
        }
        public DamageService(RunModel run, MovementSystem movement, double invulnerabilitySeconds = 0.25)
        {
            this.run = run ?? throw new ArgumentNullException(nameof(run));
            this.movement = movement ?? throw new ArgumentNullException(nameof(movement));
            if (!Positive(invulnerabilitySeconds)) throw new ArgumentOutOfRangeException(nameof(invulnerabilitySeconds));
            this.invulnerabilitySeconds = invulnerabilitySeconds; run.World.Units.Removed += Forget;
        }
        public bool TryApply(DamageRequest request, double now)
        {
            if (disposed || run.Phase != RunPhase.Running || run.Player == null || !run.Player.IsAlive || request.Key.RunId != run.Id ||
                request.Key.AttackId <= 0 || request.Key.Copy.Sign < 0 || request.Key.Repeat.Sign < 0 || !Finite(now) || now < 0 ||
                !Positive(request.Amount) || !Finite(request.KnockbackDistance) || request.KnockbackDistance < 0 || !Positive(request.KnockbackSeconds) ||
                !Finite(request.KnockbackDistance / request.KnockbackSeconds)) return false;
            int retired = RangeBefore(request.Key.AttackId);
            if (retired >= 0 && completed.Values[retired] >= request.Key.AttackId) return false;
            if (!run.World.Units.TryGet(request.TargetId, out var target) || !target.IsAlive ||
                !run.World.Units.TryGet(request.SourceId, out var source) || !source.IsAlive) return false;
            if (acceptedKeys.TryGetValue(target.Id, out var keys) && keys.Contains(request.Key)) return false;
            if (target.Kind == UnitKind.Player && now < target.InvulnerableUntil) return false;
            double protectedUntil = now + invulnerabilitySeconds;
            if (target.Kind == UnitKind.Player && (!Finite(protectedUntil) || protectedUntil <= now))
                throw new OverflowException("Invulnerability time is not representable.");
            long sequence = checked(target.HitSequence + 1);
            if (keys == null) acceptedKeys.Add(target.Id, keys = new HashSet<HitKey>());
            keys.Add(request.Key);
            if (!hitTargets.TryGetValue(request.Key.AttackId, out var targets)) hitTargets.Add(request.Key.AttackId, targets = new HashSet<long>());
            targets.Add(target.Id);
            target.Health = Math.Max(0, target.Health - request.Amount);
            if (target.Kind == UnitKind.Player) target.InvulnerableUntil = protectedUntil;
            target.LastHitAt = now; target.HitSequence = sequence;
            if (target.IsAlive && request.KnockbackDistance > 0 && request.Direction != DVec2.Zero)
                movement.AddKnockback(target.Id, request.Direction.Normalized * request.KnockbackDistance, request.KnockbackSeconds);
            Accepted?.Invoke(new CombatEvent(request, now, target.Health));
            return true;
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool Positive(double value) => value > 0 && Finite(value);
        private void Forget(UnitModel unit)
        {
            if (!acceptedKeys.TryGetValue(unit.Id, out var keys)) return;
            foreach (var key in keys)
                if (hitTargets.TryGetValue(key.AttackId, out var targets))
                { targets.Remove(unit.Id); if (targets.Count == 0) hitTargets.Remove(key.AttackId); }
            acceptedKeys.Remove(unit.Id);
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            run.World.Units.Removed -= Forget; acceptedKeys.Clear(); hitTargets.Clear(); completed.Clear(); Accepted = null;
        }
    }
}
