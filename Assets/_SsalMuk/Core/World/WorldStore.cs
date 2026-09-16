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
        private readonly SpatialIndex unitIndex = new SpatialIndex();
        private readonly SpatialIndex experienceIndex = new SpatialIndex();
        private long lastExperienceId;
        private bool disposed;
        public UnitRegistry Units { get; }
        public IReadOnlyCollection<ExperienceRecord> Experience => experience.Values;
        public int CachedChunkCount => terrain.Count;
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
        public void ClearTerrainCache() { RequireActive(); terrain.Clear(); }
        public bool EvictTerrain(ChunkCoord coord) { RequireActive(); return terrain.Remove(coord); }
        public void MoveUnit(long id, WorldPosition position)
        {
            RequireActive(); var unit = Units.Get(id);
            unit.Position = position; unitIndex.Upsert(id, position);
        }
        public long AddExperience(WorldPosition position, BigInteger value)
        {
            RequireActive(); if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            long id = checked(lastExperienceId + 1);
            experience.Add(id, new ExperienceRecord(id, position, value));
            experienceIndex.Upsert(id, position); lastExperienceId = id; return id;
        }
        public bool TryGetExperience(long id, out ExperienceRecord record) => experience.TryGetValue(id, out record);
        public bool BeginAttracting(long id)
        {
            RequireActive();
            if (!experience.TryGetValue(id, out var record) || record.State != ExperienceState.Grounded) return false;
            record.State = ExperienceState.Attracting; return true;
        }
        public void MoveExperience(long id, WorldPosition position)
        {
            RequireActive(); var record = experience[id];
            record.Position = position; experienceIndex.Upsert(id, position);
        }
        // Called by the contact-collection service; proximity alone never grants or removes a record.
        public bool TryCollectExperience(long id, out ExperienceRecord record)
        {
            RequireActive();
            if (!experience.TryGetValue(id, out record)) return false;
            record.State = ExperienceState.Collected; experience.Remove(id); experienceIndex.Remove(id); return true;
        }
        private void RequireActive() { if (disposed) throw new ObjectDisposedException(nameof(WorldStore)); }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            Units.Registered -= OnRegistered; Units.Removed -= OnRemoved;
            foreach (var unit in new List<UnitModel>(Units.Units)) Units.Remove(unit.Id);
            terrain.Clear(); experience.Clear(); unitIndex.Clear(); experienceIndex.Clear();
        }
    }
}
