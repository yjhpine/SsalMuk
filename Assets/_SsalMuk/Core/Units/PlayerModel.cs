using System.Numerics;

namespace SsalMuk.Core
{
    public sealed class PlayerModel : UnitModel
    {
        public WeaponInventory Weapons { get; } = new WeaponInventory();
        public BigInteger Level { get; internal set; } = BigInteger.One;
        internal PlayerModel(UnitSpawnRequest request) : base(request) { }
    }
}
