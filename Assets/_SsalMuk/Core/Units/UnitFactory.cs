using System;

namespace SsalMuk.Core
{
    public abstract class UnitFactory
    {
        private readonly UnitRegistry registry;
        protected UnitFactory(UnitRegistry registry) => this.registry = registry ?? throw new ArgumentNullException(nameof(registry));

        public UnitModel Spawn(UnitSpawnRequest request)
        {
            request.Validate(registry.RunId);
            UnitModel unit = CreateUnit(request);
            registry.Register(unit);
            return unit;
        }

        protected abstract UnitModel CreateUnit(UnitSpawnRequest request);
    }
}
