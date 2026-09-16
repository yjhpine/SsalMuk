using System.Numerics;

namespace SsalMuk.Core
{
    public sealed class PlayerModel : UnitModel
    {
        public WeaponInventory Weapons { get; } = new WeaponInventory();
        public BigInteger Level { get; internal set; } = BigInteger.One;
        public DVec2 MoveIntent { get; internal set; }
        public BrainState BrainState { get; internal set; }
        public long? TargetId { get; internal set; }
        public long? CollectionTargetId { get; internal set; }
        public DVec2 BreakoutDirection { get; internal set; }
        internal PlayerModel(UnitSpawnRequest request) : base(request) { }
    }
}
