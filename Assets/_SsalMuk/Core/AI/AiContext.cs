using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    // State objects consume an actor and services, not a Unity object or a player-specific view.
    public sealed class AiContext
    {
        private Dictionary<long, WorldPosition> previous = new Dictionary<long, WorldPosition>();
        private readonly List<UnitModel> enemies = new List<UnitModel>();
        private readonly Dictionary<long, DVec2> velocities = new Dictionary<long, DVec2>();
        public UnitModel Actor { get; }
        public WorldStore World { get; }
        public NavigationService Navigation { get; }
        public AiSettings Settings { get; }
        public IReadOnlyList<UnitModel> Enemies => enemies;
        public double Time { get; internal set; }
        public DVec2 MoveIntent { get; internal set; }
        public DVec2 BreakoutDirection { get; internal set; }
        public long? CollectionTargetId { get; internal set; }

        public AiContext(UnitModel actor, WorldStore world, NavigationService navigation, AiSettings settings)
        {
            Actor = actor ?? throw new ArgumentNullException(nameof(actor));
            World = world ?? throw new ArgumentNullException(nameof(world));
            Navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (actor.RunId != world.Units.RunId) throw new ArgumentException("Actor belongs to a different world.");
        }

        internal void Observe(double dt)
        {
            Time += dt; enemies.Clear(); velocities.Clear();
            var next = new Dictionary<long, WorldPosition>();
            foreach (long id in World.Query.QueryCircle(Actor.Position, Settings.SearchDistance))
            {
                if (!World.Units.TryGet(id, out var enemy) || enemy.Kind == UnitKind.Player || !enemy.IsAlive) continue;
                enemies.Add(enemy);
                velocities[id] = previous.TryGetValue(id, out var position) ? position.DisplacementTo(enemy.Position) / dt :
                    enemy.Position.DisplacementTo(Actor.Position).Normalized * enemy.Definition.MoveSpeed;
                next[id] = enemy.Position;
            }
            previous = next;
        }

        public DVec2 Direction(int index)
        {
            double angle = index * Math.PI * 2 / Settings.DirectionCount;
            return new DVec2(Math.Cos(angle), Math.Sin(angle));
        }

        public bool CanMove(DVec2 direction, double distance) => !World.Query.SweepCircle(Actor.Position, direction * distance, Actor.BodyRadius).HasValue;

        public double BlockedFraction()
        {
            int blocked = 0;
            for (int i = 0; i < Settings.DirectionCount; i++)
            {
                var direction = Direction(i); bool occupied = !CanMove(direction, Settings.ProbeDistance);
                if (!occupied) foreach (var enemy in enemies)
                {
                    if (InCorridor(enemy, direction, Settings.ProbeDistance)) { occupied = true; break; }
                }
                if (occupied) blocked++;
            }
            return (double)blocked / Settings.DirectionCount;
        }

        public bool InCorridor(UnitModel enemy, DVec2 direction, double reach)
        {
            var relative = Actor.Position.DisplacementTo(enemy.Position);
            double along = DVec2.Dot(relative, direction);
            double width = Actor.BodyRadius + enemy.BodyRadius + 0.08;
            return along >= 0 && along <= reach + enemy.BodyRadius && (relative - direction * along).Length <= width;
        }

        public bool ImminentContact()
        {
            var ownVelocity = MoveIntent * Actor.Definition.MoveSpeed;
            foreach (var enemy in enemies)
                if (ContactTime(Actor.Position.DisplacementTo(enemy.Position), velocities[enemy.Id] - ownVelocity,
                    Actor.BodyRadius + enemy.BodyRadius) <= Settings.EmergencyContactSeconds) return true;
            return false;
        }

        public double RiskAt(DVec2 offset)
        {
            double risk = 0;
            foreach (var enemy in enemies)
            {
                double clearance = (Actor.Position.DisplacementTo(enemy.Position) - offset).Length - Actor.BodyRadius - enemy.BodyRadius;
                risk += 1 / Math.Max(0.05, clearance + 0.2);
            }
            return risk;
        }

        public double EscapeRisk(DVec2 direction)
        {
            double score = RiskAt(direction * (Actor.Definition.MoveSpeed * Settings.EmergencyContactSeconds));
            foreach (var enemy in enemies)
            {
                double time = ContactTime(Actor.Position.DisplacementTo(enemy.Position),
                    velocities[enemy.Id] - direction * Actor.Definition.MoveSpeed, Actor.BodyRadius + enemy.BodyRadius);
                if (time <= Settings.EmergencyContactSeconds) score += 50 * (1 + Settings.EmergencyContactSeconds - time);
            }
            return score;
        }

        private static double ContactTime(DVec2 position, DVec2 velocity, double radius)
        {
            double c = DVec2.Dot(position, position) - radius * radius;
            if (c <= 0) return 0;
            double a = DVec2.Dot(velocity, velocity), b = DVec2.Dot(position, velocity);
            if (a < 1e-12 || b >= 0) return double.PositiveInfinity;
            double discriminant = b * b - a * c;
            return discriminant < 0 ? double.PositiveInfinity : (-b - Math.Sqrt(discriminant)) / a;
        }
    }
}
