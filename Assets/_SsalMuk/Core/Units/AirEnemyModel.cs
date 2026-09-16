namespace SsalMuk.Core
{
    public sealed class AirEnemyModel : UnitModel
    {
        public DVec2 OriginalDirection { get; internal set; }
        internal AirEnemyModel(UnitSpawnRequest request) : base(request) { OriginalDirection = request.AirDirection; }
    }
}
