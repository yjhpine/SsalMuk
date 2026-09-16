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
        private bool disposed;
        public event Action<CombatEvent> Accepted;
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
        private void Forget(UnitModel unit) => acceptedKeys.Remove(unit.Id);
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            run.World.Units.Removed -= Forget; acceptedKeys.Clear(); Accepted = null;
        }
    }
}
