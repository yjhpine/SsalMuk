using System;
using System.Collections.Generic;
using System.Numerics;

namespace SsalMuk.Core
{
    public sealed class WorldStore : IDisposable
    {
        private readonly IChunkGenerator generator;
        private readonly Dictionary<ChunkCoord, ChunkData> terrain = new Dictionary<ChunkCoord, ChunkData>();
        private readonly Dictionary<long, ExperienceRecord> experience = new Dictionary<long, ExperienceRecord>();
        private readonly HashSet<long> attractingIds = new HashSet<long>();
        private readonly SpatialIndex unitIndex = new SpatialIndex();
        private readonly SpatialIndex experienceIndex = new SpatialIndex();
        private long lastExperienceId;
        private bool disposed;
        public UnitRegistry Units { get; }
        public IReadOnlyCollection<ExperienceRecord> Experience => experience.Values;
        public IReadOnlyCollection<long> AttractingExperienceIds => attractingIds;
        public int CachedChunkCount => terrain.Count;
        public IReadOnlyCollection<ChunkData> CachedTerrain => terrain.Values;
        public long TerrainRevision { get; private set; }
        public WorldQuery Query { get; }

        public WorldStore(UnitRegistry units, IChunkGenerator generator)
        {
            Units = units ?? throw new ArgumentNullException(nameof(units));
            this.generator = generator ?? throw new ArgumentNullException(nameof(generator));
            foreach (var unit in units.Units) OnRegistered(unit);
            units.Registered += OnRegistered; units.Removed += OnRemoved;
            Query = new WorldQuery(this, unitIndex, experienceIndex);
        }
        private void OnRegistered(UnitModel unit) => unitIndex.Upsert(unit.Id, unit.Position);
        private void OnRemoved(UnitModel unit) => unitIndex.Remove(unit.Id);
        public ChunkData GetChunk(ChunkCoord coord)
        {
            RequireActive();
            if (!terrain.TryGetValue(coord, out var chunk))
            {
                chunk = generator.Generate(coord);
                if (chunk == null || !chunk.Coord.Equals(coord)) throw new InvalidOperationException("Generator returned a different chunk.");
                terrain.Add(coord, chunk);
            }
            return chunk;
        }
        public bool IsBlocked(GridCell cell) => GetChunk(cell.Chunk).IsBlocked(cell.X, cell.Y);
        public void ClearTerrainCache() { RequireActive(); terrain.Clear(); TerrainRevision = checked(TerrainRevision + 1); }
        public bool EvictTerrain(ChunkCoord coord)
        {
            RequireActive(); if (!terrain.Remove(coord)) return false;
            TerrainRevision = checked(TerrainRevision + 1); return true;
        }
        public void MoveUnit(long id, WorldPosition position)
        {
            RequireActive(); var unit = Units.Get(id);
            unit.Position = position; unitIndex.Upsert(id, position);
        }
        public long AddExperience(WorldPosition position, BigInteger value, double createdAt = 0)
        {
            RequireActive(); if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (createdAt < 0 || double.IsNaN(createdAt) || double.IsInfinity(createdAt)) throw new ArgumentOutOfRangeException(nameof(createdAt));
            long id = checked(lastExperienceId + 1);
            experience.Add(id, new ExperienceRecord(id, position, value, createdAt));
            experienceIndex.Upsert(id, position); lastExperienceId = id; return id;
        }
        public bool TryGetExperience(long id, out ExperienceRecord record) => experience.TryGetValue(id, out record);
        public bool TryBeginAttraction(Guid runId, long id, double startedAt = 0)
        {
            if (disposed || runId != Units.RunId) return false;
            if (startedAt < 0 || double.IsNaN(startedAt) || double.IsInfinity(startedAt)) throw new ArgumentOutOfRangeException(nameof(startedAt));
            if (!experience.TryGetValue(id, out var record) || record.State != ExperienceState.Grounded) return false;
            record.State = ExperienceState.Attracting; record.AttractionStartedAt = Math.Max(startedAt, record.CreatedAt);
            attractingIds.Add(id); return true;
        }
        public bool MoveExperience(Guid runId, long id, WorldPosition position)
        {
            if (disposed || runId != Units.RunId || !experience.TryGetValue(id, out var record) || record.State != ExperienceState.Attracting) return false;
            record.PreviousPosition = record.Position; record.Position = position; experienceIndex.Upsert(id, position); return true;
        }
        // Called by the contact-collection service; proximity alone never grants or removes a record.
        public bool TryCollectExperience(Guid runId, long id, out BigInteger value)
        {
            value = BigInteger.Zero;
            if (disposed || runId != Units.RunId || !experience.TryGetValue(id, out var record) || record.State != ExperienceState.Attracting) return false;
            record.State = ExperienceState.Collected; value = record.Value;
            experience.Remove(id); attractingIds.Remove(id); experienceIndex.Remove(id); return true;
        }
        private void RequireActive() { if (disposed) throw new ObjectDisposedException(nameof(WorldStore)); }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            Units.Registered -= OnRegistered; Units.Removed -= OnRemoved;
            foreach (var unit in new List<UnitModel>(Units.Units)) Units.Remove(unit.Id);
            terrain.Clear(); experience.Clear(); attractingIds.Clear(); unitIndex.Clear(); experienceIndex.Clear();
        }
    }
}
