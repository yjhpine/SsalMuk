using System;

namespace SsalMuk.Core
{
    public sealed class PlayerFactory : UnitFactory
    {
        public PlayerFactory(UnitRegistry registry) : base(registry) { }
        protected override UnitModel CreateUnit(UnitSpawnRequest request)
        {
            if (request.Kind != UnitKind.Player) throw new ArgumentException("PlayerFactory requires a player definition.");
            return new PlayerModel(request);
        }
    }
}
