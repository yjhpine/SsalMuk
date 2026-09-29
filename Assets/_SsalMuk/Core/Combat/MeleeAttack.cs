using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    internal sealed class MeleeAttack
    {
        private readonly RunModel run;
        private readonly MovementSystem movement;
        private readonly DamageService damage;
        private readonly List<long> candidates = new List<long>(64);
        private WorldPosition queryOrigin;
        private double cachedRadius;
        private long queryRevision = -1;
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
            var originStart = previousPlayer.Offset(playerTravel * startFraction);
            var originEnd = previousPlayer.Offset(playerTravel * endFraction);
            double previousProgress = Math.Max(0, Math.Min(1, (lo - attack.StartedAt) / attack.ActiveSeconds));
            double progress = hi >= attack.StartedAt + attack.ActiveSeconds ? 1 :
                Math.Max(previousProgress, Math.Min(1, (hi - attack.StartedAt) / attack.ActiveSeconds));
            double queryRadius = attack.Stats.Range + definition.Width + movement.LargestHurtRadius + movement.MaximumDisplacement + playerTravel.Length;
            var query = run.World.Query;
            // Copies in one fixed slice share this broad phase, but exact sweeps stay per attack.
            // Spawn, removal and even movement within one cell invalidate it immediately.
            if (queryRevision != query.UnitRevision || queryOrigin != originEnd || cachedRadius != queryRadius)
            {
                query.QueryCircle(originEnd, queryRadius, candidates);
                queryRevision = query.UnitRevision; queryOrigin = originEnd; cachedRadius = queryRadius;
            }
            foreach (long id in candidates)
            {
                if (!run.World.Units.TryGet(id, out var enemy) || enemy.Kind == UnitKind.Player || !enemy.IsAlive) continue;
                // An accepted hit cannot damage this target again during this copy/repeat.
                if (damage.HasAcceptedHit(id, attack.Key)) continue;
                var previousEnemy = movement.PreviousPositions.TryGetValue(id, out var old) ? old : enemy.Position;
                var enemyTravel = previousEnemy.DisplacementTo(enemy.Position);
                var relativeStart = originStart.DisplacementTo(previousEnemy.Offset(enemyTravel * startFraction));
                var relativeEnd = originEnd.DisplacementTo(previousEnemy.Offset(enemyTravel * endFraction));
                var localStart = AttackGeometry.Rotate(relativeStart, -angle); var localEnd = AttackGeometry.Rotate(relativeEnd, -angle);
                bool hit;
                if (attack.Kind == WeaponKind.Sword) hit = AttackGeometry.SwordSweepContains(localStart, localEnd, enemy.HurtRadius, attack.Stats.Range, previousProgress, progress);
                else if (attack.Kind == WeaponKind.Spear) hit = AttackGeometry.SpearSweepContains(localStart, localEnd, enemy.HurtRadius, attack.Stats.Range, definition.Width, previousProgress, progress);
                else
                {
                    hit = AttackGeometry.AxeSweepContains(localStart, localEnd, enemy.HurtRadius, attack.Stats.Range, definition.Width,
                        previousProgress * Math.PI * 2, progress * Math.PI * 2);
                }
                if (!hit) continue;
                var direction = relativeEnd != DVec2.Zero ? relativeEnd.Normalized : attack.Direction;
                damage.TryApply(new DamageRequest(attack.Key, player.Id, id, attack.Stats.Damage, direction), hi);
            }
            attack.Origin = originEnd; attack.Progress = progress;
        }
    }
}
