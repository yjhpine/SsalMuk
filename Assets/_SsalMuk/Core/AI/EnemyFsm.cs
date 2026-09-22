using System;
namespace SsalMuk.Core
{
    public sealed class EnemyFsm
    {
        private static readonly IBehaviorState[] states = { new GroundChaseBehavior(), new AirFlyBehavior(), new KnockedBackBehavior(), new DeadBehavior() };
        private static readonly AiSettings settings = new AiSettings();
        private readonly AiContext context;
        private readonly BossCharge charge;
        private EnemyState NormalState => context.Actor.Kind == UnitKind.Air ? EnemyState.FlyThrough : EnemyState.Chase;
        public EnemyState CurrentState { get; private set; }
        public DVec2 MoveIntent => context.MoveIntent;
        public EnemyFsm(UnitModel actor, IPlayerPosition player, WorldStore world, NavigationService navigation, IRandomSource chargeRandom = null)
        {
            if (actor == null || actor.Kind == UnitKind.Player) throw new ArgumentException("An enemy FSM requires an enemy actor.");
            context = new AiContext(actor, world, navigation, settings, followTarget: player);
            if (actor.Kind == UnitKind.Boss)
                charge = ((GroundEnemyModel)actor).Charge = new BossCharge(chargeRandom ?? new SeededRandom(unchecked((int)actor.Id)));
            CurrentState = NormalState; states[(int)CurrentState].Enter(context);
        }
        public void Tick(double dt)
        {
            if (dt < 0 || double.IsNaN(dt) || double.IsInfinity(dt)) throw new ArgumentOutOfRangeException(nameof(dt));
            context.Time += dt;
            if (charge != null)
            {
                if (!context.Actor.IsAlive || context.Actor.Knockback.IsActive) charge.Cancel();
                else if (charge.TryMove(context.Actor, context.FollowTarget, dt, out var displacement))
                {
                    CurrentState = charge.Phase;
                    context.MoveIntent = dt > 0 ? displacement / (context.Actor.Definition.MoveSpeed * dt) : DVec2.Zero;
                    return;
                }
            }
            var next = !context.Actor.IsAlive ? EnemyState.Dead : context.Actor.Knockback.IsActive ? EnemyState.Knockback : NormalState;
            if (next != CurrentState) { if ((int)CurrentState < states.Length) states[(int)CurrentState].Exit(context); CurrentState = next; states[(int)CurrentState].Enter(context); }
            states[(int)CurrentState].Tick(context, dt);
        }
        public DVec2 NormalDirection()
        {
            var previous = context.MoveIntent; states[(int)NormalState].Tick(context, 0); var direction = context.MoveIntent;
            context.MoveIntent = previous; return direction;
        }
    }
}
