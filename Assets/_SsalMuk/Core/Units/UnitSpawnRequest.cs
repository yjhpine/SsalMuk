using System;

namespace SsalMuk.Core
{
    public readonly struct UnitSpawnRequest
    {
        public Guid RunId { get; }
        public UnitKind Kind { get; }
        public WorldPosition Position { get; }
        public UnitDefinition Definition { get; }
        public DVec2 AirDirection { get; }

        public UnitSpawnRequest(Guid runId, UnitKind kind, WorldPosition position, UnitDefinition definition, DVec2? airDirection = null)
        {
            RunId = runId; Kind = kind; Position = position; Definition = definition; AirDirection = (airDirection ?? new DVec2(1, 0)).Normalized;
        }

        internal void Validate(Guid expectedRun)
        {
            if (RunId == Guid.Empty || RunId != expectedRun) throw new ArgumentException("Spawn request belongs to another run.");
            if (Definition == null || Definition.Kind != Kind) throw new ArgumentException("Spawn kind and definition must agree.");
            if (Kind == UnitKind.Air && AirDirection == DVec2.Zero) throw new ArgumentException("An air enemy requires a flight direction.");
        }
    }
}
