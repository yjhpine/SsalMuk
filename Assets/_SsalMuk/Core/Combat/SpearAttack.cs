namespace SsalMuk.Core
{
    public sealed class SpearAttack
    {
        private readonly MeleeAttack attack;
        public SpearAttack(RunModel run, MovementSystem movement, DamageService damage) { attack = new MeleeAttack(run, movement, damage); }
        public void Step(AttackInstance instance, double frameFrom, double frameTo, double from, double to) => attack.Step(instance, frameFrom, frameTo, from, to);
    }
}
