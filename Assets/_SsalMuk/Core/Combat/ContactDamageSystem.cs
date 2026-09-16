using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    public sealed class ContactDamageSystem
    {
        private readonly RunModel run;
        private readonly MovementSystem movement;
        private readonly DamageService damage;
        public ContactDamageSystem(RunModel run, MovementSystem movement, DamageService damage)
        {
            this.run = run ?? throw new ArgumentNullException(nameof(run)); this.movement = movement ?? throw new ArgumentNullException(nameof(movement));
            this.damage = damage ?? throw new ArgumentNullException(nameof(damage));
        }
        public void Step(double from, double to)
        {
            if (from < 0 || to < from || double.IsNaN(from) || double.IsNaN(to) || double.IsInfinity(to)) throw new ArgumentOutOfRangeException(nameof(to));
            if (run.Phase != RunPhase.Running || !run.Player.IsAlive) return;
            var player = run.Player;
            var start = movement.PreviousPositions.TryGetValue(player.Id, out var prior) ? prior : player.Position;
            var playerTravel = start.DisplacementTo(player.Position);
            var windows = new List<ContactWindow>();
            foreach (var enemy in run.World.Units.Units)
            {
                if (enemy.Kind == UnitKind.Player || !enemy.IsAlive || enemy.Definition.ContactDamage <= 0) continue;
                var enemyStart = movement.PreviousPositions.TryGetValue(enemy.Id, out var old) ? old : enemy.Position;
                var relative = start.DisplacementTo(enemyStart);
                var travel = enemyStart.DisplacementTo(enemy.Position) - playerTravel;
                if (!CircleContact.Interval(relative, travel, player.BodyRadius + enemy.BodyRadius + 1e-5, out double enter, out double exit)) continue;
                windows.Add(new ContactWindow { Enemy = enemy, Enter = from + enter * (to - from), Exit = from + exit * (to - from),
                    Relative = relative, Travel = travel });
            }
            while (player.IsAlive)
            {
                ContactWindow best = null; double at = double.PositiveInfinity;
                foreach (var window in windows)
                {
                    if (window.Finished || !window.Enemy.IsAlive) continue;
                    double next = Math.Max(window.Enter, player.InvulnerableUntil);
                    if (next > window.Exit) continue;
                    if (next < at || (next == at && (best == null || window.Enemy.Id < best.Enemy.Id))) { at = next; best = window; }
                }
                if (best == null) break;
                double fraction = to > from ? (at - from) / (to - from) : 0;
                var direction = -(best.Relative + best.Travel * fraction).Normalized;
                if (direction == DVec2.Zero) direction = new DVec2(1, 0);
                var key = new HitKey(run.Id, run.AllocateAttackId(), 0, 0);
                if (!damage.TryApply(new DamageRequest(key, best.Enemy.Id, player.Id, best.Enemy.Definition.ContactDamage, direction), at)) best.Finished = true;
                damage.CompleteAttack(key.AttackId);
            }
        }
        private sealed class ContactWindow
        { public UnitModel Enemy; public double Enter, Exit; public DVec2 Relative, Travel; public bool Finished; }
    }
}
