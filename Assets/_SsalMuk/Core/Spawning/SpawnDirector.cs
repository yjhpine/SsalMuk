using System;
using System.Collections.Generic;
namespace SsalMuk.Core
{
    public sealed class SpawnDirector : IDisposable
    {
        private readonly RunModel run;
        private readonly SpawnSettings settings;
        private readonly DifficultyCurve difficulty;
        private readonly SurroundWaveSchedule surroundWaves;
        private readonly BossSchedule bosses = new BossSchedule(SpawnSettings.BossInterval);
        private readonly IRandomSource random;
        private readonly GroundEnemyFactory groundFactory;
        private readonly AirEnemyFactory airFactory;
        private readonly List<SpawnTicket> pending = new List<SpawnTicket>();
        private readonly long[] spawned = new long[4];
        private long nextId, normalOrdinal = 1, airOrdinal = 1;
        private int cursor, normalPopulation;
        private double lastObserved, nextNormal, largestGroundRadius;
        private bool disposed;
        public IReadOnlyList<SpawnTicket> Pending { get; }
        public long SurroundWaveNumber => surroundWaves.Number;
        public int SurroundWaveStage => surroundWaves.Stage;
        public long SurroundWaveSpawnedCount { get; private set; }
        public SpawnDirector(RunModel run, SpawnSettings settings = null)
        {
            this.run = run ?? throw new ArgumentNullException(nameof(run)); this.settings = settings ?? new SpawnSettings();
            difficulty = new DifficultyCurve(this.settings); nextNormal = difficulty.NormalDeadline(1); random = run.Streams.Spawn;
            surroundWaves = new SurroundWaveSchedule(this.settings.SurroundWaves);
            groundFactory = new GroundEnemyFactory(run.World.Units); airFactory = new AirEnemyFactory(run.World.Units);
            Pending = pending.AsReadOnly(); foreach (var unit in run.Units) TrackRadius(unit); run.World.Units.Registered += TrackRadius;
            run.World.Units.Removed += OnRemoved;
        }
        public long SpawnedCount(UnitKind kind) => spawned[(int)kind];
        private void TrackRadius(UnitModel unit)
        {
            if (unit.Kind != UnitKind.Air) largestGroundRadius = Math.Max(largestGroundRadius, unit.BodyRadius);
            if (unit.Kind == UnitKind.Normal) normalPopulation++;
        }
        private void OnRemoved(UnitModel unit)
        {
            if (unit.Kind == UnitKind.Normal) normalPopulation--;
            surroundWaves.Removed(unit, Math.Max(lastObserved, run.Clock.ElapsedSeconds));
        }
        public void Tick(double now, WorldRect viewBounds)
        {
            if (disposed || run.Phase != RunPhase.Running || run.Player == null || !run.Player.IsAlive) return;
            if (now < lastObserved || double.IsNaN(now) || double.IsInfinity(now)) throw new ArgumentOutOfRangeException(nameof(now));
            foreach (var ticket in pending)
                if (ticket.IsSurroundWave && now >= ticket.Wave.ExpiresAt) ticket.Remaining = 0;
            foreach (double time in bosses.CollectDueTimes(now)) Add(UnitKind.Boss, time, 1);
            while (airOrdinal * settings.AirInterval <= now)
            {
                double at = airOrdinal * settings.AirInterval; long next = checked(airOrdinal + 1);
                if (next * settings.AirInterval <= at || double.IsInfinity(next * settings.AirInterval)) throw new NumericRangeException("Air wave deadline lost time precision.");
                Add(UnitKind.Air, at, difficulty.AirCount(at)); airOrdinal = next;
            }
            // Pending blocked spawns reserve slots, so a full population never accumulates spawn debt.
            int normalSlots = Math.Max(0, settings.NormalPopulationCap - normalPopulation);
            foreach (var ticket in pending)
                if (ticket.Kind == UnitKind.Normal && ticket.Remaining > 0)
                {
                    ticket.Remaining = Math.Min(ticket.Remaining, normalSlots);
                    normalSlots -= (int)ticket.Remaining;
                }
            // Reserve wave slots before the ordinary trickle, while sharing the same population cap.
            while (surroundWaves.IsDue(now))
            {
                surroundWaves.TryTake(now, normalPopulation, difficulty.AtSpawn(run.Definitions.GetUnit(UnitKind.Normal), now), out var wave);
                long count = Math.Min(wave.RequestedCount, normalSlots);
                if (count <= 0) continue;
                Add(UnitKind.Normal, wave.ScheduledAt, count); pending[pending.Count - 1].Wave = wave;
                normalSlots -= (int)count;
            }
            while (nextNormal <= now)
            {
                long ordinal = checked(normalOrdinal + 1); double following = difficulty.NormalDeadline(ordinal);
                if (following <= nextNormal) throw new NumericRangeException("Normal spawn schedule lost time precision.");
                if (normalSlots > 0) { Add(UnitKind.Normal, nextNormal, 1); normalSlots--; }
                normalOrdinal = ordinal; nextNormal = following;
            }
            lastObserved = now;
            int budget = settings.CreationBudget;
            foreach (var ticket in pending)
                if (ticket.Kind == UnitKind.Boss && budget > 0) Attempt(ticket, now, viewBounds, ref budget);
            foreach (var ticket in pending)
                if (ticket.IsSurroundWave && budget > 0) Attempt(ticket, now, viewBounds, ref budget);
            int visited = 0;
            while (budget > 0 && visited < pending.Count)
            {
                if (cursor >= pending.Count) cursor = 0;
                var ticket = pending[cursor++]; visited++;
                if (ticket.Kind != UnitKind.Boss && !ticket.IsSurroundWave) Attempt(ticket, now, viewBounds, ref budget);
            }
            pending.RemoveAll(ticket => ticket.Remaining == 0);
            cursor = pending.Count > 0 ? cursor % pending.Count : 0;
        }
        private void Add(UnitKind kind, double time, long count)
        { nextId = checked(nextId + 1); pending.Add(new SpawnTicket(run.Id, nextId, kind, time, count)); }
        private void Attempt(SpawnTicket ticket, double now, WorldRect view, ref int budget)
        {
            while (ticket.Remaining > 0 && budget > 0)
            {
                if (ticket.Kind == UnitKind.Normal && normalPopulation >= settings.NormalPopulationCap) { ticket.Remaining = 0; return; }
                budget--;
                if (ticket.Kind == UnitKind.Air)
                {
                    if (ticket.AirLayout == null)
                    {
                        ticket.AirDefinition = difficulty.AtSpawn(run.Definitions.GetUnit(UnitKind.Air), now);
                        ticket.AirLayout = new AirGroupSpawner(view, run.Player.Position, ticket.Count, ticket.AirDefinition.BodyRadius, settings, random);
                    }
                    airFactory.Spawn(new UnitSpawnRequest(run.Id, UnitKind.Air, ticket.AirLayout.Position(ticket.Count - ticket.Remaining),
                        ticket.AirDefinition, ticket.AirLayout.Direction));
                }
                else
                {
                    var definition = ticket.Wave?.Definition ?? difficulty.AtSpawn(run.Definitions.GetUnit(ticket.Kind), now);
                    if (!TryGroundPosition(view, definition.BodyRadius, out var position, ticket)) return;
                    var unit = groundFactory.Spawn(new UnitSpawnRequest(run.Id, ticket.Kind, position, definition));
                    if (ticket.IsSurroundWave)
                    {
                        surroundWaves.Spawned(ticket.Wave, unit);
                        SurroundWaveSpawnedCount = checked(SurroundWaveSpawnedCount + 1);
                    }
                }
                ticket.Remaining--; spawned[(int)ticket.Kind] = checked(spawned[(int)ticket.Kind] + 1);
            }
        }
        private bool TryGroundPosition(WorldRect view, double radius, out WorldPosition position, SpawnTicket ticket)
        {
            for (int attempt = 0; attempt < settings.PositionAttempts; attempt++)
            {
                DVec2 offset;
                if (ticket.IsSurroundWave)
                {
                    double margin = radius + settings.GroundMargin + AirGroupSpawner.Draw(random) * settings.GroundBand;
                    long index = ticket.Count - ticket.Remaining, sectors = Math.Min(8, ticket.Count);
                    double rows = Math.Ceiling((double)ticket.Count / sectors);
                    double within = (index / sectors + .2 + AirGroupSpawner.Draw(random) * .6) / rows;
                    double angle = (index % sectors + within) * Math.PI * 2 / sectors;
                    var direction = new DVec2(Math.Cos(angle), Math.Sin(angle));
                    double distance = Math.Min((view.HalfWidth + margin) / Math.Abs(direction.X), (view.HalfHeight + margin) / Math.Abs(direction.Y));
                    offset = direction * distance;
                }
                else
                {
                    int side = (int)(AirGroupSpawner.Draw(random) * 4);
                    double tangent = AirGroupSpawner.Draw(random) * 2 - 1;
                    double margin = radius + settings.GroundMargin + AirGroupSpawner.Draw(random) * settings.GroundBand;
                    offset = side < 2 ? new DVec2((side == 0 ? 1 : -1) * (view.HalfWidth + margin), tangent * view.HalfHeight) :
                        new DVec2(tangent * view.HalfWidth, (side == 2 ? 1 : -1) * (view.HalfHeight + margin));
                }
                var candidate = view.Center.Offset(offset);
                if (!run.World.Query.IsCircleFree(candidate, radius)) continue;
                bool occupied = false;
                foreach (long id in run.World.Query.QueryCircle(candidate, radius + largestGroundRadius + 0.1))
                    if (run.World.Units.TryGet(id, out var unit) && unit.Kind != UnitKind.Air && unit.IsAlive &&
                        candidate.DistanceTo(unit.Position) < radius + unit.BodyRadius + 0.1) { occupied = true; break; }
                if (!occupied) { position = candidate; return true; }
            }
            position = default; return false;
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            run.World.Units.Registered -= TrackRadius; run.World.Units.Removed -= OnRemoved; pending.Clear();
            surroundWaves.Clear();
        }
    }
}
