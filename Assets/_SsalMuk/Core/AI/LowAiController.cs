using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    public sealed class LowAiController
    {
        private readonly PlayerModel player;
        private readonly AiContext context;
        private readonly IBehaviorState[] states = { new CollectState(), new EvadeState(), new BreakoutState() };
        private readonly List<AiTransition> transitions = new List<AiTransition>();
        private double enteredAt, stableFor;
        private Func<double> engagementRange;
        public IReadOnlyList<AiTransition> Transitions { get; }
        public LowAiController(PlayerModel player, WorldStore world, NavigationService navigation, AiSettings settings = null, PickupSettings pickupSettings = null)
        {
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            context = new AiContext(player, world, navigation, settings ?? new AiSettings(), pickupSettings);
            Transitions = transitions.AsReadOnly(); states[0].Enter(context);
        }
        public void ConfigureEngagementRange(Func<double> range) => engagementRange = range ?? throw new ArgumentNullException(nameof(range));
        public void Tick(double dt)
        {
            if (dt <= 0 || double.IsNaN(dt) || double.IsInfinity(dt)) throw new ArgumentOutOfRangeException(nameof(dt));
            if (!player.IsAlive) { player.MoveIntent = DVec2.Zero; player.TargetId = null; return; }
            context.Observe(dt);
            if (engagementRange != null) context.EngagementRange = engagementRange();
            bool collected = player.BrainState == BrainState.Collect;
            // Predict contact using this tick's approach/braking, not last tick's full-speed chase.
            if (collected) states[(int)BrainState.Collect].Tick(context, dt);
            double blocked = context.BlockedFraction(); bool imminent = context.ImminentContact();
            if (player.BrainState == BrainState.Breakout)
            {
                stableFor = blocked <= context.Settings.ReleaseBlockedFraction ? stableFor + dt : 0;
                if (stableFor >= context.Settings.ReleaseStableSeconds && context.Time - enteredAt >= context.Settings.MinimumHoldSeconds)
                    Transition(imminent ? BrainState.Evade : BrainState.Collect, "Escape corridor is open.");
            }
            else if (blocked >= context.Settings.EnterBlockedFraction) Transition(BrainState.Breakout, "Nearby routes are blocked.");
            else if (imminent) { stableFor = 0; Transition(BrainState.Evade, "Contact is imminent."); }
            else if (player.BrainState == BrainState.Evade)
            {
                stableFor += dt;
                if (stableFor >= context.Settings.ReleaseStableSeconds && context.Time - enteredAt >= context.Settings.MinimumHoldSeconds)
                    Transition(BrainState.Collect, "Contact risk stayed low.");
            }
            if (!collected || player.BrainState != BrainState.Collect) states[(int)player.BrainState].Tick(context, dt);
            player.MoveIntent = context.MoveIntent; player.BreakoutDirection = context.BreakoutDirection;
            player.CollectionTargetId = context.CollectionTargetId;
            player.TargetId = TargetResolver.Resolve(player, context.World.Query);
        }
        private void Transition(BrainState next, string reason)
        {
            var previous = player.BrainState; if (previous == next) return;
            states[(int)previous].Exit(context); player.BrainState = next;
            enteredAt = context.Time; stableFor = 0;
            if (transitions.Count == 64) transitions.RemoveAt(0);
            transitions.Add(new AiTransition(context.Time, previous, next, reason)); states[(int)next].Enter(context);
        }
    }
}
