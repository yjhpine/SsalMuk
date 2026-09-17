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
        private readonly List<UnitModel> units = new List<UnitModel>();
        private readonly Dictionary<long, DVec2> intents = new Dictionary<long, DVec2>();
        private readonly Dictionary<long, EnemyFsm> enemies = new Dictionary<long, EnemyFsm>();
        private readonly Dictionary<long, WorldPosition> previousPositions = new Dictionary<long, WorldPosition>();
        private bool disposed;
        public IReadOnlyDictionary<long, WorldPosition> PreviousPositions { get; }
        public double LargestBodyRadius { get; private set; }
        public double LargestHurtRadius { get; private set; }
        public double MaximumDisplacement { get; private set; }
        public NavigationService Navigation => navigation;
        public EnemyFsm GetEnemyFsm(long id) => enemies.TryGetValue(id, out var fsm) ? fsm : throw new ArgumentException("No enemy FSM for this ID.");
        public MovementSystem(WorldStore world, NavigationService navigation, PlayerModel player, MovementSettings settings = null)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            if (player.RunId != world.Units.RunId) throw new ArgumentException("Player belongs to a different run.", nameof(player));
            this.settings = settings ?? new MovementSettings(); crowds = new CrowdSolver(this.settings);
            PreviousPositions = new ReadOnlyDictionary<long, WorldPosition>(previousPositions);
            world.Units.Removed += Forget;
            world.Units.Registered += TrackRadius;
            foreach (var unit in world.Units.Units) TrackRadius(unit);
        }
        public void SetMoveIntent(long id, DVec2 direction) { world.Units.Get(id); intents[id] = direction.Length > 1 ? direction.Normalized : direction; }
        public void ClearMoveIntent(long id) => intents.Remove(id);
        public void SetAirDirection(long id, DVec2 direction)
        {
            if (world.Units.Get(id).Kind != UnitKind.Air || direction.Length == 0) throw new ArgumentException("Air travel needs an air unit and a nonzero direction.");
            ((AirEnemyModel)world.Units.Get(id)).OriginalDirection = direction.Normalized;
        }
        public void AddKnockback(long id, DVec2 displacement, double seconds)
        {
            var unit = world.Units.Get(id);
            if (seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            unit.Knockback = new KnockbackState(displacement / seconds, seconds);
        }
        public void Step(double dt)
        {
            if (disposed) throw new ObjectDisposedException(nameof(MovementSystem));
            if (dt <= 0 || double.IsNaN(dt) || double.IsInfinity(dt)) throw new ArgumentOutOfRangeException(nameof(dt));
            navigation.Advance(settings.NavigationNodeBudget);
            units.Clear(); units.AddRange(world.Units.Units); units.Sort((a, b) => a.Id.CompareTo(b.Id));
            double largest = 0; previousPositions.Clear();
            foreach (var unit in units)
            {
                previousPositions[unit.Id] = unit.Position;
                unit.PreviousPosition = unit.Position;
                if (unit.Kind != UnitKind.Air && unit.IsAlive) largest = Math.Max(largest, unit.BodyRadius);
            }
            foreach (var unit in units)
            {
                enemies.TryGetValue(unit.Id, out var fsm); fsm?.Tick(dt);
                if (!unit.IsAlive) { unit.MoveIntent = DVec2.Zero; continue; }
                DVec2 direction;
                if (unit.Kind == UnitKind.Air) direction = fsm.MoveIntent;
                else if (intents.TryGetValue(unit.Id, out var input)) direction = input;
                else direction = fsm?.MoveIntent ?? DVec2.Zero;
                var knockback = unit.Knockback;
                double pushedSeconds = knockback.IsActive ? Math.Min(dt, knockback.RemainingSeconds) : 0;
                double movementSeconds = unit.Kind == UnitKind.Player ? dt : dt - pushedSeconds;
                if (fsm != null && knockback.IsActive && movementSeconds > 0)
                    direction = unit.Kind != UnitKind.Air && intents.TryGetValue(unit.Id, out var resumeInput) ? resumeInput : fsm.NormalDirection();
                unit.MoveIntent = movementSeconds > 0 ? direction : DVec2.Zero;
                var displacement = direction * (unit.Definition.MoveSpeed * movementSeconds);
                displacement = crowds.Steer(world, unit, displacement, largest);
                if (knockback.IsActive)
                {
                    displacement += knockback.Velocity * pushedSeconds;
                    unit.Knockback = new KnockbackState(knockback.Velocity, Math.Max(0, knockback.RemainingSeconds - dt));
                    if (!unit.Knockback.IsActive) fsm?.Tick(0);
                }
                if (unit.Kind == UnitKind.Air) world.MoveUnit(unit.Id, unit.Position.Offset(displacement));
                else
                {
                    world.MoveUnit(unit.Id, CircleSweep.MoveAndSlide(world.Query, unit.Position, displacement, unit.BodyRadius, settings.SlideContacts));
                }
            }
            MaximumDisplacement = 0;
            foreach (var unit in units) MaximumDisplacement = Math.Max(MaximumDisplacement, previousPositions[unit.Id].DistanceTo(unit.Position));
        }
        private void TrackRadius(UnitModel unit)
        {
            LargestBodyRadius = Math.Max(LargestBodyRadius, unit.BodyRadius);
            LargestHurtRadius = Math.Max(LargestHurtRadius, unit.HurtRadius);
            if (unit.Kind != UnitKind.Player) enemies.Add(unit.Id, new EnemyFsm(unit, player, world, navigation));
        }
        private void Forget(UnitModel unit)
        {
            if (!unit.IsAlive && enemies.TryGetValue(unit.Id, out var fsm)) fsm.Tick(0);
            intents.Remove(unit.Id); enemies.Remove(unit.Id); previousPositions.Remove(unit.Id);
            navigation.ForgetUnit(unit.Id);
        }
        public void Dispose() { if (disposed) return; disposed = true; world.Units.Removed -= Forget; world.Units.Registered -= TrackRadius; enemies.Clear(); }
    }
}
