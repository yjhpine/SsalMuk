using System;

namespace SsalMuk.Core
{
    internal sealed class MeleeAttack
    {
        private readonly RunModel run;
        private readonly MovementSystem movement;
        private readonly DamageService damage;
        public MeleeAttack(RunModel run, MovementSystem movement, DamageService damage)
        { this.run = run; this.movement = movement; this.damage = damage; }
        public void Step(AttackInstance attack, double frameFrom, double frameTo, double from, double to)
        {
            double lo = Math.Max(from, attack.StartedAt), hi = Math.Min(to, attack.StartedAt + attack.ActiveSeconds);
            if (hi < lo || !run.Player.IsAlive || run.Phase != RunPhase.Running) return;
            double startFraction = frameTo > frameFrom ? (lo - frameFrom) / (frameTo - frameFrom) : 1;
            double endFraction = frameTo > frameFrom ? (hi - frameFrom) / (frameTo - frameFrom) : 1;
            var player = run.Player; var definition = run.Definitions.GetWeapon(attack.Kind);
            var previousPlayer = movement.PreviousPositions.TryGetValue(player.Id, out var p) ? p : player.Position;
            var playerTravel = previousPlayer.DisplacementTo(player.Position);
            double angle = Math.Atan2(attack.Direction.Y, attack.Direction.X);
            var copyOffset = AttackGeometry.Rotate(CopyLayout.Offset(attack.Kind, attack.Key.Copy, definition.CopySpacing), angle);
            var originStart = previousPlayer.Offset(playerTravel * startFraction + copyOffset);
            var originEnd = previousPlayer.Offset(playerTravel * endFraction + copyOffset);
            double previousProgress = Math.Max(0, Math.Min(1, (lo - attack.StartedAt) / attack.ActiveSeconds));
            double progress = hi >= attack.StartedAt + attack.ActiveSeconds ? 1 :
                Math.Max(previousProgress, Math.Min(1, (hi - attack.StartedAt) / attack.ActiveSeconds));
            double queryRadius = attack.Stats.Range + definition.Width + movement.LargestBodyRadius + movement.MaximumDisplacement + playerTravel.Length;
            foreach (long id in run.World.Query.QueryCircle(originEnd, queryRadius))
            {
                if (!run.World.Units.TryGet(id, out var enemy) || enemy.Kind == UnitKind.Player || !enemy.IsAlive) continue;
                var previousEnemy = movement.PreviousPositions.TryGetValue(id, out var old) ? old : enemy.Position;
                var enemyTravel = previousEnemy.DisplacementTo(enemy.Position);
                var relativeStart = originStart.DisplacementTo(previousEnemy.Offset(enemyTravel * startFraction));
                var relativeEnd = originEnd.DisplacementTo(previousEnemy.Offset(enemyTravel * endFraction));
                var localStart = AttackGeometry.Rotate(relativeStart, -angle); var localEnd = AttackGeometry.Rotate(relativeEnd, -angle);
                bool hit;
                if (attack.Kind == WeaponKind.Sword) hit = AttackGeometry.SwordSweepContains(localStart, localEnd, enemy.BodyRadius, attack.Stats.Range, previousProgress, progress);
                else if (attack.Kind == WeaponKind.Spear) hit = AttackGeometry.SpearSweepContains(localStart, localEnd, enemy.BodyRadius, attack.Stats.Range, definition.Width, previousProgress, progress);
                else
                {
                    double phase = CopyLayout.Phase(attack.Key.Copy);
                    hit = AttackGeometry.AxeSweepContains(localStart, localEnd, enemy.BodyRadius, attack.Stats.Range, definition.Width,
                        phase + previousProgress * Math.PI * 2, phase + progress * Math.PI * 2);
                }
                if (!hit) continue;
                var direction = relativeEnd != DVec2.Zero ? relativeEnd.Normalized : attack.Direction;
                damage.TryApply(new DamageRequest(attack.Key, player.Id, id, attack.Stats.Damage, direction), hi);
            }
            attack.Origin = originEnd; attack.Progress = progress;
        }
    }
}
