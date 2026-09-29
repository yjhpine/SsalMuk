using System;

namespace SsalMuk.Core
{
    public sealed class BossCharge
    {
        public const double WindupSeconds = .5, CooldownSeconds = 5, RepeatChance = .3, ChargeSpeed = 30;
        private enum ChargeLeg { FirstPass, ExtraPass, Approach }
        private readonly IRandomSource random;
        private double remainingWindup, remainingDistance, cooldown;
        private bool legQueued;
        private ChargeLeg currentLeg, queuedLeg;
        public EnemyState Phase { get; private set; } = EnemyState.Chase;
        public bool HasSuperArmor => legQueued || Phase == EnemyState.Telegraph || Phase == EnemyState.Charge;
        public WorldPosition Origin { get; private set; }
        public WorldPosition End { get; private set; }
        public DVec2 Direction { get; private set; }
        public double Length { get; private set; }
        public bool LastMoveWasCharge { get; private set; }
        public BossCharge(IRandomSource random) => this.random = random ?? throw new ArgumentNullException(nameof(random));

        // Returns a displacement, so the final charge step cannot overshoot the locked endpoint.
        public bool TryMove(UnitModel actor, IPlayerPosition target, double dt, out DVec2 displacement)
        {
            displacement = DVec2.Zero; LastMoveWasCharge = false;
            if (dt < 0 || double.IsNaN(dt) || double.IsInfinity(dt)) throw new ArgumentOutOfRangeException(nameof(dt));
            if (!actor.IsAlive || actor.Knockback.IsActive || !target.IsAlive)
            {
                Cancel();
                // Being pushed blocks charging, but does not pause or restart the wait between charges.
                cooldown = Math.Max(0, cooldown - dt);
                return false;
            }
            if (dt == 0) return Phase != EnemyState.Chase;
            if (Phase == EnemyState.Chase)
            {
                if (cooldown > 1e-9) { cooldown = Math.Max(0, cooldown - dt); return false; }
                var delta = actor.Position.DisplacementTo(target.Position);
                if (delta.Length < 1e-6) { Cancel(); return false; }
                currentLeg = legQueued ? queuedLeg : ChargeLeg.FirstPass; legQueued = false;
                double distanceMultiplier = currentLeg == ChargeLeg.Approach ? 1 : 2;
                Origin = actor.Position; Direction = delta.Normalized; Length = delta.Length * distanceMultiplier;
                End = Origin.Offset(delta * distanceMultiplier); remainingDistance = Length;
                remainingWindup = WindupSeconds; Phase = EnemyState.Telegraph;
            }
            double activeSeconds = dt;
            if (Phase == EnemyState.Telegraph)
            {
                double waiting = Math.Min(remainingWindup, dt);
                remainingWindup -= waiting; activeSeconds -= waiting;
                if (remainingWindup <= 1e-9) Phase = EnemyState.Charge;
                if (activeSeconds <= 1e-9) return true;
            }
            double distance = Math.Min(remainingDistance, ChargeSpeed * activeSeconds);
            displacement = Direction * distance; remainingDistance -= distance;
            LastMoveWasCharge = distance > 0;
            if (remainingDistance <= 1e-9)
            {
                legQueued = currentLeg != ChargeLeg.Approach;
                if (legQueued)
                    queuedLeg = currentLeg == ChargeLeg.FirstPass && random.NextUnit() < RepeatChance
                        ? ChargeLeg.ExtraPass : ChargeLeg.Approach;
                cooldown = legQueued ? 0 : CooldownSeconds; Phase = EnemyState.Chase;
            }
            return true;
        }

        public void Cancel()
        {
            if (Phase == EnemyState.Chase && !legQueued) return;
            Phase = EnemyState.Chase; cooldown = CooldownSeconds; legQueued = false; remainingDistance = 0;
        }
    }
}
