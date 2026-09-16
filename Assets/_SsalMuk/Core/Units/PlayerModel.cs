using System.Numerics;

namespace SsalMuk.Core
{
    public sealed class PlayerModel : UnitModel
    {
        public WeaponInventory Weapons { get; private set; } = new WeaponInventory();
        public GrowthState Growth { get; private set; } = new GrowthState();
        public BigInteger Level => Growth.Level;
        public BrainState BrainState { get; internal set; }
        public long? TargetId { get; internal set; }
        public long? CollectionTargetId { get; internal set; }
        public DVec2 BreakoutDirection { get; internal set; }
        internal PlayerModel(UnitSpawnRequest request) : base(request) { }
        internal void ResetGrowth() { Growth = new GrowthState(); Weapons = new WeaponInventory(); }
    }
}
