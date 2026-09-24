using System;

namespace SsalMuk.Core
{
    public enum CollectionTactic { Nearby, Lure, Flank, Return }

    // Tactical memory survives emergency FSM states. It never owns or moves loot.
    public sealed class LootRecovery
    {
        public CollectionTactic Tactic { get; private set; }
        public long? TargetId { get; private set; }
        public bool Active => Tactic != CollectionTactic.Nearby;
        private GridCell area;
        private WorldPosition escapeStart, pivot, waypoint;
        private DVec2 escapeDirection;
        private double nextPlan, radius;
        private int turn;
        internal static GridCell AreaAt(WorldPosition position) => new GridCell(position.Chunk,
            (int)(position.Local.X / 4) * 4, (int)(position.Local.Y / 4) * 4);
        internal static WorldPosition AreaCenter(GridCell cell) => new WorldPosition(cell.Chunk, new DVec2(cell.X + 2, cell.Y + 2));

        public void Remember(ExperienceRecord record)
        {
            if (Active || record == null) return;
            TargetId = record.Id; area = AreaAt(record.Position);
        }
        public void Clear()
        { TargetId = null; Tactic = CollectionTactic.Nearby; turn = 0; escapeDirection = DVec2.Zero; }

        public bool TryGetTarget(AiContext context, out ExperienceRecord record)
        {
            if (TargetId.HasValue && context.World.TryGetExperience(TargetId.Value, out record) && record.State == ExperienceState.Grounded) return true;
            record = null;
            if (TargetId.HasValue)
                foreach (long id in context.World.Query.QueryExperienceCircle(AreaCenter(area), 3))
                {
                    if (!context.World.TryGetExperience(id, out var candidate) || candidate.State != ExperienceState.Grounded || !AreaAt(candidate.Position).Equals(area)) continue;
                    if (record == null || candidate.Id < record.Id) record = candidate;
                }
            if (record != null) { TargetId = record.Id; return true; }
            Clear(); return false;
        }
        public void Interrupt(AiContext context)
        {
            if (!TryGetTarget(context, out _)) return;
            Tactic = CollectionTactic.Lure; escapeStart = context.Actor.Position;
            escapeDirection = DVec2.Zero; nextPlan = 0;
        }
        public void TrackEscape(AiContext context)
        {
            if (!Active || !TryGetTarget(context, out _)) return;
            if (context.MoveIntent.Length > .1) escapeDirection = context.MoveIntent.Normalized;
        }
        // Return delegates actual terrain navigation to CollectState.
        public bool TrySteer(AiContext context, out DVec2 intent)
        {
            intent = DVec2.Zero;
            if (!Active || !TryGetTarget(context, out var loot)) return false;
            if (context.Time + 1e-9 >= nextPlan)
            { nextPlan = context.Time + context.Settings.CollectionPlanSeconds; Plan(context, loot.Position); }
            if (Tactic == CollectionTactic.Return) return false;
            var delta = context.Actor.Position.DisplacementTo(waypoint);
            if (delta.Length < .05) return true;
            intent = delta.Normalized;
            if (!context.CanMove(intent, Math.Min(delta.Length, context.Actor.MoveSpeed * context.Settings.EmergencyContactSeconds)))
            { nextPlan = 0; intent = DVec2.Zero; }
            return true;
        }
        private void Plan(AiContext context, WorldPosition loot)
        {
            var toward = context.Actor.Position.DisplacementTo(loot); var axis = toward.Normalized;
            var center = DVec2.Zero; int blocking = 0;
            foreach (var enemy in context.Enemies)
            {
                if (enemy.Kind == UnitKind.Air) continue;
                var relative = context.RelativeTo(enemy);
                double along = DVec2.Dot(relative, axis), width = context.Actor.BodyRadius + enemy.BodyRadius + .65;
                if (along <= 0 || along > toward.Length || (relative - axis * along).Length > width) continue;
                center += relative; blocking++;
            }
            if (blocking == 0 || toward.Length <= context.PickupRadius)
            { Tactic = CollectionTactic.Return; return; }
            if (Tactic == CollectionTactic.Lure && escapeStart.DistanceTo(context.Actor.Position) < context.Settings.ProbeDistance * 2)
            {
                var direction = escapeDirection == DVec2.Zero ? -axis : escapeDirection;
                if (context.CanMove(direction, context.Settings.ProbeDistance))
                { waypoint = context.Actor.Position.Offset(direction * context.Settings.ProbeDistance); return; }
            }
            if (Tactic != CollectionTactic.Flank)
            {
                pivot = context.Actor.Position.Offset(center / blocking);
                radius = Math.Max(context.Settings.ProbeDistance, pivot.DistanceTo(context.Actor.Position));
                Tactic = CollectionTactic.Flank;
            }
            var radial = pivot.DisplacementTo(context.Actor.Position);
            if (radial.Length < .01) radial = -axis;
            var tangent = new DVec2(-radial.Y, radial.X).Normalized;
            if (turn == 0) turn = FlankCost(context, tangent) <= FlankCost(context, -tangent) ? 1 : -1;
            var desired = (tangent * turn + radial.Normalized * Math.Max(-.6, Math.Min(.6, (radius - radial.Length) / radius))).Normalized;
            if (!context.CanMove(desired, context.Settings.ProbeDistance))
            {
                if (context.CanMove(-tangent * turn, context.Settings.ProbeDistance)) { turn = -turn; desired = tangent * turn; }
                else
                {
                    double best = double.PositiveInfinity; desired = DVec2.Zero;
                    for (int i = 0; i < context.Settings.DirectionCount; i++)
                    {
                        var candidate = context.Direction(i);
                        double score = FlankCost(context, candidate) + 5 * (1 - DVec2.Dot(candidate, tangent * turn));
                        if (score < best) { best = score; desired = candidate; }
                    }
                }
            }
            waypoint = context.Actor.Position.Offset(desired * context.Settings.ProbeDistance);
        }
        private static double FlankCost(AiContext context, DVec2 direction) =>
            !context.CanMove(direction, context.Settings.ProbeDistance) ? double.PositiveInfinity :
            context.RiskAt(direction * context.Settings.ProbeDistance) + context.EscapeRisk(direction);
    }
}
