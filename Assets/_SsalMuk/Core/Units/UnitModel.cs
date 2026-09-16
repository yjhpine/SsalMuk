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
        public double InvulnerableUntil { get; internal set; }
        public KnockbackState Knockback { get; internal set; }
        public double LastHitAt { get; internal set; } = double.NegativeInfinity;
        public long HitSequence { get; internal set; }
        public WorldPosition Position { get; internal set; }
        public DVec2 MoveIntent { get; internal set; }

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
