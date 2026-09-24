namespace SsalMuk.Core
{
    public static class TargetResolver
    {
        private static readonly AiSettings defaults = new AiSettings();
        public static long? Resolve(PlayerModel player, IWorldQuery world, AiSettings settings = null)
        {
            if (!player.IsAlive) return null;
            settings = settings ?? defaults;
            var nearby = world.QueryCircle(player.Position, settings.SearchDistance);
            long? best = null; double distance = double.PositiveInfinity;
            if (player.BrainState == BrainState.Breakout && player.BreakoutDirection != DVec2.Zero)
            {
                foreach (long id in nearby)
                {
                    if (!world.TryGetUnit(id, out var enemy) || enemy.Kind == UnitKind.Player || !enemy.IsAlive) continue;
                    var relative = player.Position.DisplacementTo(enemy.Position);
                    double along = DVec2.Dot(relative, player.BreakoutDirection);
                    if (along < 0 || (relative - player.BreakoutDirection * along).Length > player.BodyRadius + enemy.BodyRadius + 0.08) continue;
                    double length = relative.Length;
                    if (length < distance || (length == distance && (!best.HasValue || id < best.Value))) { best = id; distance = length; }
                }
                if (best.HasValue) return best;
            }
            foreach (long id in nearby)
            {
                if (!world.TryGetUnit(id, out var enemy) || enemy.Kind == UnitKind.Player || !enemy.IsAlive) continue;
                double score = WeightedDistance(player, enemy, settings);
                if (score < distance || (score == distance && (!best.HasValue || id < best.Value))) { best = id; distance = score; }
            }
            return best ?? world.FindNearestEnemy(player.Position);
        }

        internal static double WeightedDistance(UnitModel actor, UnitModel enemy, AiSettings settings) =>
            actor.Position.DistanceTo(enemy.Position) / (enemy.Kind == UnitKind.Boss ? settings.BossTargetWeight : 1);
    }
}
