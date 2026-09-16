using System;

namespace SsalMuk.Core
{
    public sealed class AirEnemyFactory : UnitFactory
    {
        public AirEnemyFactory(UnitRegistry registry) : base(registry) { }
        protected override UnitModel CreateUnit(UnitSpawnRequest request)
        {
            if (request.Kind != UnitKind.Air) throw new ArgumentException("AirEnemyFactory requires an air definition.");
            return new AirEnemyModel(request);
        }
    }
}
