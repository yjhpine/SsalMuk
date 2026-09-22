using System;

namespace SsalMuk.Core
{
    public sealed class BossCharge
    {
        public const double WindupSeconds = .5, CooldownSeconds = 5, RepeatChance = .3;
        private readonly IRandomSource random;
        private double remainingWindup, remainingDistance, cooldown, speed;
        private bool repeatQueued, extraCharge;
        public EnemyState Phase { get; private set; } = EnemyState.Chase;
        public WorldPosition Origin { get; private set; }
        public WorldPosition End { get; private set; }
        public DVec2 Direction { get; private set; }
        public double Length { get; private set; }
        public BossCharge(IRandomSource random) => this.random = random ?? throw new ArgumentNullException(nameof(random));

        // Returns a displacement, so the final charge step cannot overshoot the locked endpoint.
        public bool TryMove(UnitModel actor, IPlayerPosition target, double dt, out DVec2 displacement)
        {
            displacement = DVec2.Zero;
            if (dt < 0 || double.IsNaN(dt) || double.IsInfinity(dt)) throw new ArgumentOutOfRangeException(nameof(dt));
            if (!actor.IsAlive || actor.Knockback.IsActive || !target.IsAlive) { Cancel(); return true; }
            if (dt == 0) return Phase != EnemyState.Chase;
            if (Phase == EnemyState.Chase)
            {
                if (cooldown > 1e-9) { cooldown = Math.Max(0, cooldown - dt); return false; }
                var delta = actor.Position.DisplacementTo(target.Position);
                if (delta.Length < 1e-6) { Cancel(); return false; }
                extraCharge = repeatQueued; repeatQueued = false;
                Origin = actor.Position; Direction = delta.Normalized; Length = delta.Length * 2;
                End = Origin.Offset(delta * 2); remainingDistance = Length;
                speed = target.MoveSpeed * 2; remainingWindup = WindupSeconds; Phase = EnemyState.Telegraph;
            }
            double activeSeconds = dt;
            if (Phase == EnemyState.Telegraph)
            {
                double waiting = Math.Min(remainingWindup, dt);
                remainingWindup -= waiting; activeSeconds -= waiting;
                if (remainingWindup <= 1e-9) Phase = EnemyState.Charge;
                if (activeSeconds <= 1e-9) return true;
            }
            double distance = Math.Min(remainingDistance, speed * activeSeconds);
            displacement = Direction * distance; remainingDistance -= distance;
            if (remainingDistance <= 1e-9)
            {
                repeatQueued = !extraCharge && random.NextUnit() < RepeatChance;
                cooldown = repeatQueued ? 0 : CooldownSeconds; Phase = EnemyState.Chase;
            }
            return true;
        }

        public void Cancel()
        { Phase = EnemyState.Chase; cooldown = CooldownSeconds; repeatQueued = false; remainingDistance = 0; }
    }
}
