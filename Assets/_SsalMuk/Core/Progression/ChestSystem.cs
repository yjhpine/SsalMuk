using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    public sealed class ChestRecord
    {
        public long Id { get; }
        public WorldPosition Position { get; }
        public bool Opened { get; internal set; }
        internal ChestRecord(long id, WorldPosition position) { Id = id; Position = position; }
    }

    public sealed class ChestSystem
    {
        private readonly RunModel run;
        private readonly Dictionary<ChunkCoord, ChestRecord> byChunk = new Dictionary<ChunkCoord, ChestRecord>();
        private readonly Dictionary<long, ChestRecord> byId = new Dictionary<long, ChestRecord>();
        private bool enabled;
        private long nextId;
        private ChunkCoord? prepared;
        public const double OpenRadius = .85;
        public ChestRecord Target { get; internal set; }
        public ChestSystem(RunModel run) => this.run = run ?? throw new ArgumentNullException(nameof(run));
        public void Enable() { enabled = true; Prepare(); }
        // Local dictionary lookups only: exploring farther does not increase per-frame work.
        public IEnumerable<ChestRecord> Nearby(WorldPosition origin)
        {
            if (!enabled || run.Phase == RunPhase.Disposed) yield break;
            for (long y = -1; y <= 1; y++) for (long x = -1; x <= 1; x++)
                if (byChunk.TryGetValue(new ChunkCoord(checked(origin.Chunk.X + x), checked(origin.Chunk.Y + y)), out var chest) && chest != null)
                    yield return chest;
        }
        private void Prepare()
        {
            if (!enabled || run.Player == null || run.Phase == RunPhase.Disposed) return;
            var center = run.Player.Position.Chunk;
            if (prepared == center) return;
            for (long y = -1; y <= 1; y++) for (long x = -1; x <= 1; x++)
            {
                var coord = new ChunkCoord(checked(center.X + x), checked(center.Y + y));
                if (!run.World.ContainsTerrain(coord) || byChunk.ContainsKey(coord)) continue;
                var random = new SeededRandom(unchecked((int)StableHash.Combine(run.Seed, coord.X, coord.Y, 1, 103)));
                var anchor = new WorldPosition(coord, new DVec2(16.5, 16.5));
                double radius = run.Player.BodyRadius;
                ChestRecord chest = null;
                // Require a clear body-width line to the generators' connected central lane.
                for (int attempt = 0; attempt < 48; attempt++)
                {
                    var position = new WorldPosition(coord, new DVec2(4.5 + (int)(random.NextUnit() * 23), 5.5 + (int)(random.NextUnit() * 21)));
                    if (position.DistanceTo(anchor) < 4 || !run.World.Query.IsCircleFree(anchor, radius) ||
                        !run.World.Query.IsCircleFree(position, radius) || run.World.Query.SweepCircle(anchor, anchor.DisplacementTo(position), radius).HasValue) continue;
                    chest = new ChestRecord(checked(++nextId), position); byId.Add(chest.Id, chest); break;
                }
                byChunk.Add(coord, chest);
            }
            prepared = center;
        }
        public bool TryOpen(long id)
        {
            if (!enabled || run.Phase != RunPhase.Running || run.IsPaused || run.Player == null || !run.Player.IsAlive ||
                !byId.TryGetValue(id, out var chest) || chest.Opened) return false;
            var delta = run.Player.Position.DisplacementTo(chest.Position);
            if (delta.Length > OpenRadius || run.World.Query.SweepCircle(run.Player.Position, delta, run.Player.BodyRadius).HasValue) return false;
            if (!run.Rewards.AddChestChoice()) return false;
            chest.Opened = true; if (ReferenceEquals(Target, chest)) Target = null; return true;
        }
        public void Step()
        {
            if (!enabled || run.Phase != RunPhase.Running || run.IsPaused || run.Player == null || !run.Player.IsAlive) return;
            Prepare();
            foreach (var chest in Nearby(run.Player.Position)) if (!chest.Opened) TryOpen(chest.Id);
        }
    }
}
