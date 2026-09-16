using SsalMuk.Core;
namespace SsalMuk.Presentation
{
    public interface ICombatWorldView : IWorldView
    {
        void SetFrameTime(double time);
        void ShowExperience(ExperienceRecord experience, ExperienceTier tier, DVec2 relative, double radius);
        void ShowAttack(AttackShapeSnapshot shape, DVec2 relative);
        void ShowProjectile(ProjectileModel projectile, DVec2 relative);
        void ShowExplosion(ExplosionSnapshot explosion, DVec2 relative, double lifetime);
    }
}
