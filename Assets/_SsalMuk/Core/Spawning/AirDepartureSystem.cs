using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    public sealed class AirDepartureSystem : IDisposable
    {
        private readonly RunModel run;
        private readonly double margin;
        private readonly Dictionary<long, AirEnemyModel> airborne = new Dictionary<long, AirEnemyModel>();
        private readonly List<long> departing = new List<long>();
        private bool disposed;
        public long DepartedCount { get; private set; }

        public AirDepartureSystem(RunModel run, double margin)
        {
            this.run = run ?? throw new ArgumentNullException(nameof(run));
            WeaponDefinition.RequirePositive(margin, nameof(margin)); this.margin = margin;
            foreach (var unit in run.Units) Track(unit);
            run.World.Units.Registered += Track; run.World.Units.Removed += Forget;
        }

        public void Step(WorldRect view)
        {
            if (disposed || run.Phase != RunPhase.Running) return;
            departing.Clear();
            foreach (var air in airborne.Values)
            {
                // Incoming waves also start offscreen. Only retire the outgoing half of their flight.
                if (!air.IsAlive || air.Knockback.IsActive || view.Contains(air.Position, margin + air.BodyRadius)) continue;
                if (DVec2.Dot(view.Center.DisplacementTo(air.Position), air.OriginalDirection) > 0) departing.Add(air.Id);
            }
            foreach (long id in departing)
            {
                long next = checked(DepartedCount + 1);
                if (!run.World.Units.Remove(id)) continue;
                DepartedCount = next;
                if (run.Player.TargetId == id) run.Player.TargetId = null;
            }
        }

        private void Track(UnitModel unit) { if (unit is AirEnemyModel air) airborne.Add(air.Id, air); }
        private void Forget(UnitModel unit) => airborne.Remove(unit.Id);
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            run.World.Units.Registered -= Track; run.World.Units.Removed -= Forget;
            airborne.Clear(); departing.Clear();
        }
    }
}
