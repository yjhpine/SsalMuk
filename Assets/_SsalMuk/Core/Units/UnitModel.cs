using System;

namespace SsalMuk.Core
{
    public abstract class UnitModel
    {
        public long Id { get; private set; }
        public Guid RunId { get; }
        public UnitDefinition Definition { get; }
        public UnitKind Kind => Definition.Kind;
        public double BodyRadius => Definition.BodyRadius;
        public double Health { get; internal set; }
        public bool IsAlive => Health > 0;
        public WorldPosition Position { get; internal set; }

        internal UnitModel(UnitSpawnRequest request)
        {
            RunId = request.RunId; Definition = request.Definition;
            Position = request.Position; Health = Definition.MaxHealth;
        }

        internal void AssignIdentity(long id)
        {
            if (Id != 0 || id <= 0) throw new InvalidOperationException("Unit identity can only be assigned once.");
            Id = id;
        }
    }
}
