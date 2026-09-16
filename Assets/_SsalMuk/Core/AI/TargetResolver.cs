namespace SsalMuk.Core
{
    public static class TargetResolver
    {
        public static long? Resolve(PlayerModel player, IWorldQuery world)
        {
            if (!player.IsAlive) return null;
            long? best = null; double distance = double.PositiveInfinity;
            if (player.BrainState == BrainState.Breakout && player.BreakoutDirection != DVec2.Zero)
            {
                foreach (long id in world.QueryCircle(player.Position, 12))
                {
                    if (!world.TryGetUnit(id, out var enemy) || enemy.Kind == UnitKind.Player || !enemy.IsAlive) continue;
                    var relative = player.Position.DisplacementTo(enemy.Position);
                    double along = DVec2.Dot(relative, player.BreakoutDirection);
                    if (along < 0 || (relative - player.BreakoutDirection * along).Length > player.BodyRadius + enemy.BodyRadius + 0.08) continue;
                    double length = relative.Length;
                    if (length < distance || (length == distance && (!best.HasValue || id < best.Value))) { best = id; distance = length; }
                }
            }
            return best ?? world.FindNearestEnemy(player.Position);
        }
    }
}
