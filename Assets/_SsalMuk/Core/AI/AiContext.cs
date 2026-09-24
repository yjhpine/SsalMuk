using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    // State objects consume an actor and services, not a Unity object or a player-specific view.
    public sealed class AiContext
    {
        private Dictionary<long, WorldPosition> previous = new Dictionary<long, WorldPosition>();
        private Dictionary<long, WorldPosition> next = new Dictionary<long, WorldPosition>();
        private readonly List<UnitModel> enemies = new List<UnitModel>();
        private readonly List<long> nearbyIds = new List<long>();
        private readonly Dictionary<long, DVec2> relatives = new Dictionary<long, DVec2>();
        private readonly Dictionary<long, DVec2> velocities = new Dictionary<long, DVec2>();
        private readonly List<Threat> threats = new List<Threat>();
        private readonly List<ChargeLane> chargeLanes = new List<ChargeLane>();
        private readonly DVec2[] directions;
        public UnitModel Actor { get; }
        public WorldStore World { get; }
        public NavigationService Navigation { get; }
        public AiSettings Settings { get; }
        public PickupSettings PickupSettings { get; }
        public double PickupRadius => PickupSettings.AttractionRadius * (Actor is PlayerModel player ? player.Upgrades.PickupMultiplier : 1);
        public IPlayerPosition FollowTarget { get; }
        public IReadOnlyList<UnitModel> Enemies => enemies;
        public UnitModel EngagementTarget { get; private set; }
        public double Time { get; internal set; }
        public DVec2 MoveIntent { get; internal set; }
        public DVec2 BreakoutDirection { get; internal set; }
        public long? CollectionTargetId { get; internal set; }
        public double EngagementRange { get; internal set; } = 1.6;

        public AiContext(UnitModel actor, WorldStore world, NavigationService navigation, AiSettings settings, PickupSettings pickupSettings = null, IPlayerPosition followTarget = null)
        {
            Actor = actor ?? throw new ArgumentNullException(nameof(actor));
            World = world ?? throw new ArgumentNullException(nameof(world));
            Navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            directions = new DVec2[settings.DirectionCount];
            for (int i = 0; i < directions.Length; i++)
            { double angle = i * Math.PI * 2 / directions.Length; directions[i] = new DVec2(Math.Cos(angle), Math.Sin(angle)); }
            PickupSettings = pickupSettings ?? PickupSettings.TestDefaults(); PickupSettings.ValidateForPlayer(actor.BodyRadius);
            FollowTarget = followTarget;
            if (actor.RunId != world.Units.RunId) throw new ArgumentException("Actor belongs to a different world.");
        }

        internal void Observe(double dt)
        {
            Time += dt; enemies.Clear(); velocities.Clear(); relatives.Clear(); threats.Clear(); chargeLanes.Clear(); next.Clear(); EngagementTarget = null;
            double bestScore = double.PositiveInfinity;
            World.Query.QueryCircle(Actor.Position, Settings.SearchDistance, nearbyIds);
            foreach (long id in nearbyIds)
            {
                if (!World.Units.TryGet(id, out var enemy) || enemy.Kind == UnitKind.Player || !enemy.IsAlive) continue;
                enemies.Add(enemy);
                var relative = Actor.Position.DisplacementTo(enemy.Position);
                relatives[id] = relative;
                velocities[id] = previous.TryGetValue(id, out var position) ? position.DisplacementTo(enemy.Position) / dt :
                    -relative.Normalized * enemy.MoveSpeed;
                threats.Add(new Threat { Position = relative, Velocity = velocities[id], Radius = Actor.BodyRadius + enemy.BodyRadius });
                if (enemy is GroundEnemyModel ground && ground.Charge != null && ground.Charge.HasSuperArmor)
                    chargeLanes.Add(new ChargeLane { Start = relative, End = Actor.Position.DisplacementTo(ground.Charge.End), Radius = Actor.BodyRadius + enemy.BodyRadius + .15 });
                double score = relative.Length / (enemy.Kind == UnitKind.Boss ? Settings.BossTargetWeight : 1);
                if (score < bestScore || (score == bestScore && (EngagementTarget == null || enemy.Id < EngagementTarget.Id)))
                { EngagementTarget = enemy; bestScore = score; }
                next[id] = enemy.Position;
            }
            var swap = previous; previous = next; next = swap;
        }

        public DVec2 Direction(int index) => directions[index];
        public DVec2 RelativeTo(UnitModel enemy) => relatives.TryGetValue(enemy.Id, out var relative) ? relative : Actor.Position.DisplacementTo(enemy.Position);

        public bool CanMove(DVec2 direction, double distance) => !World.Query.SweepCircle(Actor.Position, direction * distance, Actor.BodyRadius).HasValue;

        public DVec2 EngageNearestEnemy()
        {
            var nearest = EngagementTarget;
            if (nearest == null) return DVec2.Zero;
            double distance = Actor.Position.DistanceTo(nearest.Position);
            if (distance < 1e-10) return DVec2.Zero;
            // Stay inside the weapon's real hit reach, with room between contact bodies.
            double desired = Math.Max(Actor.BodyRadius + nearest.BodyRadius + .12, EngagementRange + nearest.HurtRadius * .5 - .15);
            var toward = Actor.Position.DisplacementTo(nearest.Position).Normalized;
            // Brake before reaching the target distance; emergency prediction then uses that same intent.
            double speed = Math.Max(-.35, Math.Min(1, (distance - desired) / (Actor.MoveSpeed * Settings.EmergencyContactSeconds)));
            var intent = toward * speed;
            return CanMove(intent, Actor.MoveSpeed * Settings.EmergencyContactSeconds) ? intent : DVec2.Zero;
        }

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
            var relative = RelativeTo(enemy);
            double along = DVec2.Dot(relative, direction);
            double width = Actor.BodyRadius + enemy.BodyRadius + 0.08;
            return along >= 0 && along <= reach + enemy.BodyRadius && (relative - direction * along).Length <= width;
        }

        public bool ImminentContact()
        {
            var ownVelocity = MoveIntent * Actor.MoveSpeed;
            double horizon = Settings.EmergencyContactSeconds;
            double protectedFor = Math.Max(0, Actor.InvulnerableUntil - Time);
            foreach (var threat in threats)
                if (CircleContact.Interval(threat.Position, (threat.Velocity - ownVelocity) * horizon,
                    threat.Radius, out _, out double exit) && exit * horizon >= protectedFor) return true;
            return false;
        }

        public double RiskAt(DVec2 offset)
        {
            double risk = 0;
            foreach (var threat in threats)
            {
                double clearance = (threat.Position - offset).Length - threat.Radius;
                risk += 1 / Math.Max(0.05, clearance + 0.2);
            }
            return risk;
        }

        public double EscapeRisk(DVec2 direction)
        {
            double score = RiskAt(direction * (Actor.MoveSpeed * Settings.EmergencyContactSeconds));
            foreach (var threat in threats)
            {
                double time = ContactTime(threat.Position, threat.Velocity - direction * Actor.MoveSpeed, threat.Radius);
                if (time <= Settings.EmergencyContactSeconds) score += 50 * (1 + Settings.EmergencyContactSeconds - time);
            }
            var offset = direction * (Actor.MoveSpeed * Settings.EmergencyContactSeconds);
            foreach (var lane in chargeLanes) score += 1000 / (.1 + LaneDistance(lane, offset));
            return score;
        }

        public bool ImminentCharge()
        {
            var offset = MoveIntent * (Actor.MoveSpeed * Settings.EmergencyContactSeconds);
            foreach (var lane in chargeLanes)
                if (LaneDistance(lane, DVec2.Zero) <= lane.Radius || LaneDistance(lane, offset) <= lane.Radius) return true;
            return false;
        }

        private static double LaneDistance(ChargeLane lane, DVec2 point)
        {
            var segment = lane.End - lane.Start;
            double lengthSquared = DVec2.Dot(segment, segment);
            double along = lengthSquared < 1e-12 ? 0 : Math.Max(0, Math.Min(1, DVec2.Dot(point - lane.Start, segment) / lengthSquared));
            return (point - lane.Start - segment * along).Length;
        }

        private struct Threat { public DVec2 Position, Velocity; public double Radius; }
        private struct ChargeLane { public DVec2 Start, End; public double Radius; }

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
