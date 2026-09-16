using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SsalMuk.Core
{
    public sealed class MovementSystem : IDisposable
    {
        private readonly WorldStore world;
        private readonly NavigationService navigation;
        private readonly PlayerModel player;
        private readonly CrowdSolver crowds;
        private readonly MovementSettings settings;
        private readonly Dictionary<long, DVec2> intents = new Dictionary<long, DVec2>();
        private readonly Dictionary<long, DVec2> airDirections = new Dictionary<long, DVec2>();
        private readonly Dictionary<long, Knockback> knockbacks = new Dictionary<long, Knockback>();
        private readonly Dictionary<long, WorldPosition> previousPositions = new Dictionary<long, WorldPosition>();
        private bool disposed;
        public IReadOnlyDictionary<long, WorldPosition> PreviousPositions { get; }
        public MovementSystem(WorldStore world, NavigationService navigation, PlayerModel player, MovementSettings settings = null)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            if (player.RunId != world.Units.RunId) throw new ArgumentException("Player belongs to a different run.", nameof(player));
            this.settings = settings ?? new MovementSettings(); crowds = new CrowdSolver(this.settings);
            PreviousPositions = new ReadOnlyDictionary<long, WorldPosition>(previousPositions);
            world.Units.Removed += Forget;
        }
        public void SetMoveIntent(long id, DVec2 direction) { world.Units.Get(id); intents[id] = direction.Length > 1 ? direction.Normalized : direction; }
        public void ClearMoveIntent(long id) => intents.Remove(id);
        public void SetAirDirection(long id, DVec2 direction)
        {
            if (world.Units.Get(id).Kind != UnitKind.Air || direction.Length == 0) throw new ArgumentException("Air travel needs an air unit and a nonzero direction.");
            airDirections[id] = direction.Normalized;
        }
        public void AddKnockback(long id, DVec2 displacement, double seconds)
        {
            world.Units.Get(id);
            if (seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            knockbacks[id] = new Knockback { Velocity = displacement / seconds, Remaining = seconds };
        }
        public void Step(double dt)
        {
            if (disposed) throw new ObjectDisposedException(nameof(MovementSystem));
            if (dt <= 0 || double.IsNaN(dt) || double.IsInfinity(dt)) throw new ArgumentOutOfRangeException(nameof(dt));
            navigation.Advance(settings.NavigationNodeBudget);
            var units = new List<UnitModel>(world.Units.Units); units.Sort((a, b) => a.Id.CompareTo(b.Id));
            double largest = 0; previousPositions.Clear();
            foreach (var unit in units)
            {
                previousPositions[unit.Id] = unit.Position;
                if (unit.Kind != UnitKind.Air && unit.IsAlive) largest = Math.Max(largest, unit.BodyRadius);
            }
            foreach (var unit in units)
            {
                if (!unit.IsAlive) continue;
                DVec2 direction;
                if (unit.Kind == UnitKind.Air) direction = airDirections.TryGetValue(unit.Id, out var air) ? air : new DVec2(1, 0);
                else if (intents.TryGetValue(unit.Id, out var input)) direction = input;
                else direction = unit.Kind == UnitKind.Player || !player.IsAlive ? DVec2.Zero : navigation.ChaseDirection(unit, player.Position);
                var displacement = direction * (unit.Definition.MoveSpeed * dt);
                if (knockbacks.TryGetValue(unit.Id, out var knockback))
                {
                    displacement += knockback.Velocity * Math.Min(dt, knockback.Remaining);
                    knockback.Remaining -= dt;
                    if (knockback.Remaining <= 0) knockbacks.Remove(unit.Id); else knockbacks[unit.Id] = knockback;
                }
                if (unit.Kind == UnitKind.Air) world.MoveUnit(unit.Id, unit.Position.Offset(displacement));
                else
                {
                    displacement = crowds.Constrain(world, unit, displacement, largest);
                    world.MoveUnit(unit.Id, CircleSweep.MoveAndSlide(world.Query, unit.Position, displacement, unit.BodyRadius, settings.SlideContacts));
                }
            }
            crowds.Resolve(world, dt);
        }
        private void Forget(UnitModel unit)
        {
            intents.Remove(unit.Id); airDirections.Remove(unit.Id); knockbacks.Remove(unit.Id); previousPositions.Remove(unit.Id);
            navigation.ForgetUnit(unit.Id); crowds.Forget(unit.Id);
        }
        public void Dispose() { if (disposed) return; disposed = true; world.Units.Removed -= Forget; }
        private struct Knockback { public DVec2 Velocity; public double Remaining; }
    }
}
