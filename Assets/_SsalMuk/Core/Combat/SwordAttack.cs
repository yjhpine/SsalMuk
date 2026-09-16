using System;

namespace SsalMuk.Core
{
    public sealed class SwordAttack
    {
        private readonly RunModel run;
        private readonly MovementSystem movement;
        private readonly DamageService damage;
        public SwordAttack(RunModel run, MovementSystem movement, DamageService damage)
        { this.run = run; this.movement = movement; this.damage = damage; }
        public void Step(AttackInstance attack, double frameFrom, double frameTo, double from, double to)
        {
            double lo = Math.Max(from, attack.StartedAt), hi = Math.Min(to, attack.StartedAt + attack.ActiveSeconds);
            if (hi < lo || !run.Player.IsAlive || run.Phase != RunPhase.Running) return;
            double startFraction = frameTo > frameFrom ? (lo - frameFrom) / (frameTo - frameFrom) : 1;
            double endFraction = frameTo > frameFrom ? (hi - frameFrom) / (frameTo - frameFrom) : 1;
            var player = run.Player;
            var previousPlayer = movement.PreviousPositions.TryGetValue(player.Id, out var p) ? p : player.Position;
            var playerTravel = previousPlayer.DisplacementTo(player.Position);
            var originStart = previousPlayer.Offset(playerTravel * startFraction);
            var originEnd = previousPlayer.Offset(playerTravel * endFraction);
            double previousProgress = Math.Max(0, Math.Min(1, (lo - attack.StartedAt) / attack.ActiveSeconds));
            double progress = Math.Max(previousProgress, Math.Min(1, (hi - attack.StartedAt) / attack.ActiveSeconds));
            double angle = -Math.Atan2(attack.Direction.Y, attack.Direction.X);
            double queryRadius = attack.Stats.Range + movement.LargestBodyRadius + movement.MaximumDisplacement + playerTravel.Length;
            foreach (long id in run.World.Query.QueryCircle(originEnd, queryRadius))
            {
                if (!run.World.Units.TryGet(id, out var enemy) || enemy.Kind == UnitKind.Player || !enemy.IsAlive) continue;
                var previousEnemy = movement.PreviousPositions.TryGetValue(id, out var old) ? old : enemy.Position;
                var enemyTravel = previousEnemy.DisplacementTo(enemy.Position);
                var relativeStart = originStart.DisplacementTo(previousEnemy.Offset(enemyTravel * startFraction));
                var relativeEnd = originEnd.DisplacementTo(previousEnemy.Offset(enemyTravel * endFraction));
                if (!AttackGeometry.SwordSweepContains(AttackGeometry.Rotate(relativeStart, angle), AttackGeometry.Rotate(relativeEnd, angle),
                    enemy.BodyRadius, attack.Stats.Range, previousProgress, progress)) continue;
                var direction = relativeEnd != DVec2.Zero ? relativeEnd.Normalized : attack.Direction;
                damage.TryApply(new DamageRequest(attack.Key, player.Id, id, attack.Stats.Damage, direction), hi);
            }
            attack.Origin = originEnd; attack.Progress = progress;
        }
    }
}
