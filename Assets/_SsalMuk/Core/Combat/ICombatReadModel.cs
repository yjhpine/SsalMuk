using System.Collections.Generic;
namespace SsalMuk.Core
{
    public interface ICombatReadModel
    {
        IEnumerable<AttackShapeSnapshot> AttackShapes { get; }
        IReadOnlyList<ProjectileModel> Projectiles { get; }
        IReadOnlyList<ExplosionSnapshot> Explosions { get; }
    }
}
