using System;

namespace SsalMuk.Core
{
    public sealed class GroundEnemyFactory : UnitFactory
    {
        public GroundEnemyFactory(UnitRegistry registry) : base(registry) { }
        protected override UnitModel CreateUnit(UnitSpawnRequest request)
        {
            if (request.Kind != UnitKind.Normal && request.Kind != UnitKind.Boss)
                throw new ArgumentException("GroundEnemyFactory requires a normal or boss definition.");
            return new GroundEnemyModel(request);
        }
    }
}
