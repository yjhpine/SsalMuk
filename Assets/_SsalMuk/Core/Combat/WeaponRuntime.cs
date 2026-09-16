using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    public sealed class WeaponRuntime : IDisposable
    {
        private readonly RunModel run;
        private readonly WeaponDefinition definition;
        private readonly Func<WeaponStats> stats;
        private readonly SwordAttack sword;
        private readonly DamageService damage;
        private readonly AttackScheduler scheduler = new AttackScheduler();
        private readonly List<AttackInstance> active = new List<AttackInstance>();
        private bool disposed;
        private DVec2 lastDirection = new DVec2(1, 0);
        public event Action<AttackInstance> Launched;
        public IReadOnlyList<AttackInstance> ActiveAttacks { get; }
        public long LaunchCount { get; private set; }
        public WeaponRuntime(RunModel run, WeaponKind kind, MovementSystem movement, DamageService damage, Func<WeaponStats> stats = null)
        {
            this.run = run ?? throw new ArgumentNullException(nameof(run)); this.damage = damage; definition = run.Definitions.GetWeapon(kind);
            if (kind != WeaponKind.Sword) throw new NotSupportedException("This attack executor is added in C3: " + kind);
            this.stats = stats ?? (() => definition.BaseStats); sword = new SwordAttack(run, movement, damage);
            ActiveAttacks = active.AsReadOnly();
        }
        public void Tick(double from, double to)
        {
            if (disposed || run.Phase != RunPhase.Running || !run.Player.IsAlive) return;
            if (from < 0 || to < from || double.IsNaN(from) || double.IsNaN(to) || double.IsInfinity(to)) throw new ArgumentOutOfRangeException(nameof(to));
            double cursor = from;
            while (cursor < to)
            {
                double end = Math.Min(to, cursor + run.Clock.FixedStep);
                if (end <= cursor) throw new OverflowException("Attack simulation time lost precision.");
                double at = cursor;
                foreach (double due in scheduler.CollectDueTimes(cursor, end, stats().PeriodSeconds, TargetResolver.Resolve(run.Player, run.World.Query).HasValue))
                {
                    AdvanceAttacks(from, to, at, due); at = due;
                    long? target = TargetResolver.Resolve(run.Player, run.World.Query);
                    if (!target.HasValue) { scheduler.Reset(); break; }
                    var delta = run.Player.Position.DisplacementTo(run.World.Units.Get(target.Value).Position);
                    if (delta != DVec2.Zero) lastDirection = delta.Normalized;
                    var snapshot = stats();
                    var instance = new AttackInstance(new HitKey(run.Id, run.AllocateAttackId(), 0, 0), definition.Kind,
                        due, definition.ActiveSeconds, lastDirection, snapshot, run.Player.Position);
                    active.Add(instance); LaunchCount = checked(LaunchCount + 1); Launched?.Invoke(instance);
                }
                AdvanceAttacks(from, to, at, end); cursor = end;
            }
        }
        private void AdvanceAttacks(double frameFrom, double frameTo, double from, double to)
        {
            foreach (var attack in active) sword.Step(attack, frameFrom, frameTo, from, to);
            for (int i = active.Count - 1; i >= 0; i--)
                if (active[i].Progress >= 1) { damage.CompleteAttack(active[i].Key.AttackId); active.RemoveAt(i); }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            foreach (var attack in active) damage.CompleteAttack(attack.Key.AttackId);
            active.Clear(); scheduler.Reset(); Launched = null;
        }
    }
}
