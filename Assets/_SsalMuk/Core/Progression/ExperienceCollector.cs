using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    public sealed class ExperienceCollector
    {
        private readonly RunModel run;
        private readonly MovementSystem movement;
        private readonly ProgressionService progression;
        private readonly HashSet<long> candidateIds = new HashSet<long>();
        private readonly List<long> orderedIds = new List<long>();
        public ExperienceCollector(RunModel run, MovementSystem movement, ProgressionService progression)
        {
            this.run = run ?? throw new ArgumentNullException(nameof(run));
            this.movement = movement ?? throw new ArgumentNullException(nameof(movement));
            this.progression = progression ?? throw new ArgumentNullException(nameof(progression));
        }
        public void Step(double dt)
        {
            if (dt <= 0 || double.IsNaN(dt) || double.IsInfinity(dt)) throw new ArgumentOutOfRangeException(nameof(dt));
            if (run.Phase != RunPhase.Running || run.Player == null || !run.Player.IsAlive) return;
            var player = run.Player; var settings = run.PickupSettings; var world = run.World;
            var previous = movement.PreviousPositions.TryGetValue(player.Id, out var position) ? position : player.Position;
            var travel = previous.DisplacementTo(player.Position);
            double to = run.Clock.ElapsedSeconds, from = to - dt;
            candidateIds.Clear(); orderedIds.Clear();
            foreach (long id in world.AttractingExperienceIds) candidateIds.Add(id);
            foreach (long id in world.Query.QueryExperienceCircle(player.Position, settings.AttractionRadius + travel.Length)) candidateIds.Add(id);
            orderedIds.AddRange(candidateIds); orderedIds.Sort();
            foreach (long id in orderedIds)
            {
                if (!world.TryGetExperience(id, out var orb) || orb.CreatedAt > to) continue;
                double fraction = Math.Max(0, (orb.CreatedAt - from) / dt);
                if (orb.State == ExperienceState.Grounded)
                {
                    var playerAtCreation = previous.Offset(travel * fraction);
                    if (!CircleContact.Interval(playerAtCreation.DisplacementTo(orb.Position), -travel * (1 - fraction),
                        settings.AttractionRadius, out double enter, out _)) continue;
                    fraction += enter * (1 - fraction);
                    if (!world.TryBeginAttraction(run.Id, id, from + fraction * dt)) continue;
                }
                if (orb.State != ExperienceState.Attracting) continue;
                fraction = Math.Max(fraction, Math.Max(0, (orb.AttractionStartedAt - from) / dt));
                if (fraction > 1) continue;
                var playerAtStart = previous.Offset(travel * fraction);
                var towardPlayer = orb.Position.DisplacementTo(player.Position);
                var flight = towardPlayer.Normalized * Math.Min(towardPlayer.Length, settings.FlightSpeed * dt * (1 - fraction));
                if (CircleContact.Interval(playerAtStart.DisplacementTo(orb.Position), flight - travel * (1 - fraction),
                    player.BodyRadius + settings.OrbRadius, out _, out _) && world.TryCollectExperience(run.Id, id, out var value))
                    progression.AddExperience(value);
                else world.MoveExperience(run.Id, id, orb.Position.Offset(flight));
            }
        }
    }
}
