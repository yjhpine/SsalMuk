using System;

namespace SsalMuk.Core
{
    public sealed class PowerupSystem : IDisposable
    {
        private readonly RunModel run;
        private readonly MovementSystem movement;
        private readonly DeathService death;
        private readonly IRandomSource random;
        private readonly PowerupSettings settings;
        private bool disposed;
        public PowerupSystem(RunModel run, MovementSystem movement, DeathService death,
            PowerupSettings settings = null, IRandomSource random = null)
        {
            this.run = run ?? throw new ArgumentNullException(nameof(run));
            this.movement = movement ?? throw new ArgumentNullException(nameof(movement));
            this.death = death ?? throw new ArgumentNullException(nameof(death));
            this.settings = settings ?? new PowerupSettings();
            this.random = random ?? new SeededRandom(unchecked((int)StableHash.Combine(run.Seed, 0, 0, 1, 991)));
            death.EnemyKilled += Drop;
        }
        private bool Active => !disposed && run.Phase == RunPhase.Running && run.Player != null && run.Player.IsAlive;
        private void Drop(UnitModel enemy)
        {
            if (!Active || random.NextUnit() >= settings.DropChance) return;
            var kind = (PowerupKind)Math.Min(3, (int)(random.NextUnit() * 4));
            var position = DropPlacement.NearestFreeFloor(run.World, enemy.Position, run.Player.BodyRadius);
            run.World.AddItem(kind, position);
        }
        public void UpdateEffects(double dt)
        {
            if (!(dt > 0) || double.IsInfinity(dt)) throw new ArgumentOutOfRangeException(nameof(dt));
            if (!Active) return;
            run.Player.Effects.Advance(run.Clock.ElapsedSeconds);
            run.Player.Health = Math.Min(run.Player.Definition.MaxHealth, run.Player.Health + run.Player.Upgrades.HealingPerSecond * dt);
        }
        public void CollectAfterMovement()
        {
            if (!Active) return;
            var player = run.Player;
            var previous = movement.PreviousPositions.TryGetValue(player.Id, out var start) ? start : player.Position;
            var travel = previous.DisplacementTo(player.Position);
            double radius = run.PickupSettings.AttractionRadius * player.Upgrades.PickupMultiplier;
            foreach (long id in run.World.QueryItems(player.Position, radius + travel.Length))
            {
                if (!run.World.TryGetItem(id, out var item) ||
                    !CircleContact.Interval(previous.DisplacementTo(item.Position), -travel, radius, out _, out _) ||
                    !run.World.RemoveItem(run.Id, id)) continue;
                Apply(item.Kind);
            }
        }
        private void Apply(PowerupKind kind)
        {
            var player = run.Player; double now = run.Clock.ElapsedSeconds;
            if (kind == PowerupKind.Magnet)
            {
                foreach (var orb in run.World.Experience)
                {
                    run.World.TryBeginAttraction(run.Id, orb.Id, now);
                    // A distant saved chunk should return its orbs promptly, still by physical contact.
                    orb.MagnetFlightSpeed = Math.Max(orb.MagnetFlightSpeed, orb.Position.DistanceTo(player.Position) / 1.5 + player.MoveSpeed);
                }
            }
            else
            {
                player.Effects.Refresh(kind, now);
                if (kind == PowerupKind.Invincibility)
                    player.InvulnerableUntil = Math.Max(player.InvulnerableUntil, player.Effects.InvincibilityUntil);
            }
        }
        public void Dispose() { if (disposed) return; disposed = true; death.EnemyKilled -= Drop; }
    }
}
