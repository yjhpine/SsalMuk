using System;
using System.Collections.Generic;
namespace SsalMuk.Core
{
    public sealed class ProjectileSystem : IDisposable
    {
        private readonly RunModel run;
        private readonly MovementSystem movement;
        private readonly DamageService damage;
        private readonly WeaponDefinition definition;
        private readonly List<ProjectileModel> active = new List<ProjectileModel>();
        private readonly List<ExplosionSnapshot> explosions = new List<ExplosionSnapshot>();
        private long lastLaunchedId;
        private bool disposed;
        public event Action<ExplosionSnapshot> Exploded;
        public IReadOnlyList<ProjectileModel> Active { get; }
        public IReadOnlyList<ExplosionSnapshot> Explosions { get; }
        public ProjectileSystem(RunModel run, MovementSystem movement, DamageService damage)
        {
            this.run = run ?? throw new ArgumentNullException(nameof(run)); this.movement = movement ?? throw new ArgumentNullException(nameof(movement));
            this.damage = damage ?? throw new ArgumentNullException(nameof(damage)); definition = run.Definitions.GetWeapon(WeaponKind.Fireball);
            Active = active.AsReadOnly(); Explosions = explosions.AsReadOnly();
        }
        public ProjectileModel Launch(AttackInstance attack)
        {
            if (disposed || run.Phase != RunPhase.Running || !run.Player.IsAlive) throw new InvalidOperationException("A projectile requires a living run.");
            if (attack == null || attack.Kind != WeaponKind.Fireball || attack.Key.RunId != run.Id || attack.Key.AttackId <= lastLaunchedId ||
                attack.Key.Copy < 0 || attack.Key.Repeat < 0 || attack.Direction == DVec2.Zero || attack.Stats == null ||
                attack.StartedAt < 0 || double.IsNaN(attack.StartedAt) || double.IsInfinity(attack.StartedAt)) throw new ArgumentException("Invalid or repeated projectile launch.");
            var projectile = new ProjectileModel(attack, definition); active.Add(projectile); lastLaunchedId = attack.Key.AttackId; return projectile;
        }
        public void Step(double from, double to) => Step(from, to, from, to);
        public void Step(double frameFrom, double frameTo, double from, double to)
        {
            if (disposed || run.Phase != RunPhase.Running || !run.Player.IsAlive) return;
            if (from < 0 || to < from || double.IsNaN(from) || double.IsNaN(to) || double.IsInfinity(to) || frameFrom > from || frameTo < to)
                throw new ArgumentOutOfRangeException(nameof(to));
            foreach (var shot in active)
            {
                double lo = Math.Max(from, Math.Max(shot.SpawnedAt, shot.UpdatedAt)), hi = Math.Min(to, shot.ExpiresAt);
                if (hi < lo) continue;
                if (lo > shot.UpdatedAt + 1e-9) throw new InvalidOperationException("Projectile updates must not skip flight intervals.");
                var origin = shot.Position; var travel = shot.Velocity * (hi - lo); shot.PreviousPosition = origin;
                double a = Fraction(lo, frameFrom, frameTo), b = Fraction(hi, frameFrom, frameTo);
                double earliest = double.PositiveInfinity; long? first = null;
                double queryRadius = travel.Length + movement.MaximumDisplacement + movement.LargestHurtRadius + shot.Radius;
                foreach (long id in run.World.Query.QueryCircle(origin, queryRadius))
                {
                    if (!run.World.Units.TryGet(id, out var unit) || unit.Kind == UnitKind.Player || !unit.IsAlive) continue;
                    var previous = Previous(unit); var enemyTravel = previous.DisplacementTo(unit.Position);
                    var relative = origin.DisplacementTo(previous.Offset(enemyTravel * a));
                    if (!CircleContact.Interval(relative, enemyTravel * (b - a) - travel, unit.HurtRadius + shot.Radius, out double enter, out _)) continue;
                    if (enter < earliest || (enter == earliest && (!first.HasValue || id < first.Value))) { earliest = enter; first = id; }
                }
                shot.UpdatedAt = hi;
                if (first.HasValue)
                {
                    shot.Position = origin.Offset(travel * earliest); shot.HitTargetId = first; shot.IsAlive = false;
                    Explode(shot, lo + (hi - lo) * earliest, frameFrom, frameTo);
                }
                else { shot.Position = origin.Offset(travel); if (hi >= shot.ExpiresAt) shot.IsAlive = false; }
                if (!shot.IsAlive) damage.CompleteAttack(shot.Key.AttackId);
            }
            active.RemoveAll(shot => !shot.IsAlive);
            explosions.RemoveAll(explosion => explosion.Time + definition.ActiveSeconds < to);
        }
        private static double Fraction(double time, double from, double to) => to > from ? Math.Max(0, Math.Min(1, (time - from) / (to - from))) : 1;
        private WorldPosition Previous(UnitModel unit) => movement.PreviousPositions.TryGetValue(unit.Id, out var previous) ? previous : unit.Position;
        private void Explode(ProjectileModel shot, double time, double frameFrom, double frameTo)
        {
            double fraction = Fraction(time, frameFrom, frameTo);
            foreach (long id in run.World.Query.QueryCircle(shot.Position, shot.Stats.Range + movement.LargestHurtRadius + movement.MaximumDisplacement))
            {
                if (!run.World.Units.TryGet(id, out var unit) || unit.Kind == UnitKind.Player || !unit.IsAlive) continue;
                var previous = Previous(unit); var atImpact = previous.Offset(previous.DisplacementTo(unit.Position) * fraction);
                var relative = shot.Position.DisplacementTo(atImpact);
                if (relative.Length > shot.Stats.Range + unit.HurtRadius + 1e-10) continue;
                damage.TryApply(new DamageRequest(shot.Key, run.Player.Id, unit.Id, shot.Stats.Damage,
                    relative == DVec2.Zero ? shot.Direction : relative.Normalized), time);
            }
            var explosion = new ExplosionSnapshot(shot.Key, shot.Position, shot.Stats.Range, time, shot.HitTargetId.Value, definition.ActiveSeconds);
            explosions.Add(explosion); Exploded?.Invoke(explosion);
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            foreach (var shot in active) { shot.IsAlive = false; damage.CompleteAttack(shot.Key.AttackId); }
            active.Clear(); explosions.Clear(); Exploded = null;
        }
    }
}
