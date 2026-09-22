namespace SsalMuk.Core
{
    public sealed class GroundEnemyModel : UnitModel
    {
        public BossCharge Charge { get; internal set; }
        internal GroundEnemyModel(UnitSpawnRequest request) : base(request) { }
    }
}
