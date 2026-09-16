namespace SsalMuk.Core
{
    public sealed class FireballAttack
    {
        private readonly ProjectileSystem projectiles;
        public FireballAttack(ProjectileSystem projectiles) { this.projectiles = projectiles; }
        public ProjectileModel Launch(AttackInstance attack) => projectiles.Launch(attack);
    }
}
