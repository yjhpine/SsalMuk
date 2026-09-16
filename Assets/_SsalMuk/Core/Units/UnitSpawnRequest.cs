using System;

namespace SsalMuk.Core
{
    public readonly struct UnitSpawnRequest
    {
        public Guid RunId { get; }
        public UnitKind Kind { get; }
        public WorldPosition Position { get; }
        public UnitDefinition Definition { get; }

        public UnitSpawnRequest(Guid runId, UnitKind kind, WorldPosition position, UnitDefinition definition)
        {
            RunId = runId; Kind = kind; Position = position; Definition = definition;
        }

        internal void Validate(Guid expectedRun)
        {
            if (RunId == Guid.Empty || RunId != expectedRun) throw new ArgumentException("Spawn request belongs to another run.");
            if (Definition == null || Definition.Kind != Kind) throw new ArgumentException("Spawn kind and definition must agree.");
        }
    }
}
