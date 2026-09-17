using System;
using System.Collections.Generic;
using System.Numerics;

namespace SsalMuk.Core
{
    public sealed class WeaponRuntime : IDisposable
    {
        private readonly RunModel run;
        private readonly WeaponDefinition definition;
        private readonly Func<WeaponStats> stats;
        private readonly SwordAttack sword;
        private readonly SpearAttack spear;
        private readonly AxeAttack axe;
        private readonly FireballAttack fireball;
        private readonly MovementSystem movement;
        private readonly DamageService damage;
        private readonly AttackScheduler scheduler = new AttackScheduler();
        private readonly List<AttackInstance> active = new List<AttackInstance>();
        private bool disposed;
        private DVec2 lastDirection = new DVec2(1, 0);
        public event Action<AttackInstance> Launched;
        public IReadOnlyList<AttackInstance> ActiveAttacks { get; }
        public ProjectileSystem Projectiles { get; }
        public WeaponKind Kind => definition.Kind;
        public long LaunchCount { get; private set; }
        public BigInteger PendingStrikes => scheduler.PendingStrikes;
        public double MaximumDispatchDelay { get; private set; }
        public WeaponRuntime(RunModel run, WeaponKind kind, MovementSystem movement, DamageService damage, Func<WeaponStats> stats = null)
        {
            this.run = run ?? throw new ArgumentNullException(nameof(run)); this.damage = damage; this.movement = movement; definition = run.Definitions.GetWeapon(kind);
            this.stats = stats ?? (() => StatCalculator.Calculate(definition, run.Player.Weapons.Get(kind), run.GrowthSettings));
            sword = new SwordAttack(run, movement, damage); spear = new SpearAttack(run, movement, damage); axe = new AxeAttack(run, movement, damage);
            if (kind == WeaponKind.Fireball) { Projectiles = new ProjectileSystem(run, movement, damage); fireball = new FireballAttack(Projectiles); }
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
                foreach (var reserved in scheduler.CollectDueStrikes(cursor, end, stats, TargetResolver.Resolve(run.Player, run.World.Query).HasValue))
                {
                    double due = reserved.Time;
                    MaximumDispatchDelay = Math.Max(MaximumDispatchDelay, to - due);
                    AdvanceAttacks(from, to, at, due); at = due;
                    long? target = TargetResolver.Resolve(run.Player, run.World.Query);
                    if (!target.HasValue) { scheduler.Reset(); break; }
                    var delta = run.Player.Position.DisplacementTo(run.World.Units.Get(target.Value).Position);
                    if (delta != DVec2.Zero) lastDirection = delta.Normalized;
                    var snapshot = stats();
                    var previous = movement.PreviousPositions.TryGetValue(run.Player.Id, out var previousPlayer) ? previousPlayer : run.Player.Position;
                    double fraction = to > from ? Math.Max(0, Math.Min(1, (due - from) / (to - from))) : 1;
                    var origin = previous.Offset(previous.DisplacementTo(run.Player.Position) * fraction);
                    for (BigInteger copy = 0; copy < snapshot.Copies; copy++)
                    {
                        var offset = AttackGeometry.Rotate(CopyLayout.Offset(definition.Kind, copy, definition.CopySpacing), Math.Atan2(lastDirection.Y, lastDirection.X));
                        var instance = new AttackInstance(new HitKey(run.Id, run.AllocateAttackId(), copy, reserved.Repeat), definition.Kind,
                            due, definition.ActiveSeconds, lastDirection, snapshot, origin.Offset(offset));
                        active.Add(instance); LaunchCount = checked(LaunchCount + 1);
                        fireball?.Launch(instance); Launched?.Invoke(instance);
                    }
                }
                AdvanceAttacks(from, to, at, end); cursor = end;
            }
        }
        private void AdvanceAttacks(double frameFrom, double frameTo, double from, double to)
        {
            foreach (var attack in active)
            {
                if (definition.Kind == WeaponKind.Sword) sword.Step(attack, frameFrom, frameTo, from, to);
                else if (definition.Kind == WeaponKind.Spear) spear.Step(attack, frameFrom, frameTo, from, to);
                else if (definition.Kind == WeaponKind.Axe) axe.Step(attack, frameFrom, frameTo, from, to);
                else
                {
                    attack.Progress = Math.Max(0, Math.Min(1, (to - attack.StartedAt) / attack.ActiveSeconds));
                    var offset = AttackGeometry.Rotate(CopyLayout.Offset(definition.Kind, attack.Key.Copy, definition.CopySpacing), Math.Atan2(attack.Direction.Y, attack.Direction.X));
                    attack.Origin = run.Player.Position.Offset(offset);
                }
            }
            Projectiles?.Step(frameFrom, frameTo, from, to);
            for (int i = active.Count - 1; i >= 0; i--)
                if (active[i].Progress >= 1) { if (definition.Kind != WeaponKind.Fireball) damage.CompleteAttack(active[i].Key.AttackId); active.RemoveAt(i); }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            foreach (var attack in active) damage.CompleteAttack(attack.Key.AttackId);
            Projectiles?.Dispose();
            active.Clear(); scheduler.Reset(); Launched = null;
        }
    }
}
