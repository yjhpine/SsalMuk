using System;

namespace SsalMuk.Core
{
    public sealed class RunSimulation : IDisposable
    {
        private readonly RunModel run;
        private readonly MovementSystem movement;
        private readonly LowAiController ai;
        private readonly DeathService death;
        private readonly ContactDamageSystem contact;
        private bool disposed;
        public WeaponRuntime Sword { get; }
        public ProgressionService Progression { get; }
        public ExperienceCollector Collector { get; }
        public RunSimulation(RunModel run, MovementSystem movement, LowAiController ai, DamageService damage, DeathService death,
            ContactDamageSystem contact)
        {
            this.run = run; this.movement = movement; this.ai = ai; this.death = death; this.contact = contact;
            Progression = new ProgressionService(run); Collector = new ExperienceCollector(run, movement, Progression);
            Sword = new WeaponRuntime(run, WeaponKind.Sword, movement, damage, () =>
                StatCalculator.Calculate(run.Definitions.GetWeapon(WeaponKind.Sword), run.Player.Weapons.Get(WeaponKind.Sword), run.GrowthSettings));
        }
        public void Step(double dt)
        {
            if (disposed || run.Phase != RunPhase.Running) return;
            double count = dt / run.Clock.FixedStep;
            if (dt < 0 || double.IsNaN(dt) || double.IsInfinity(dt) || count > int.MaxValue || Math.Abs(count - Math.Round(count)) > 1e-8)
                throw new ArgumentOutOfRangeException(nameof(dt), "Simulation uses whole fixed steps.");
            for (int i = 0; i < (int)Math.Round(count); i++)
            {
                if (!run.Player.IsAlive) { Finish(); break; }
                double from = run.Clock.ElapsedSeconds; run.Clock.Advance(); double to = run.Clock.ElapsedSeconds;
                if (ai != null) { ai.Tick(run.Clock.FixedStep); movement.SetMoveIntent(run.Player.Id, run.Player.MoveIntent); }
                movement.Step(run.Clock.FixedStep);
                Sword.Tick(from, to); death.Flush(); contact.Step(from, to);
                if (!run.Player.IsAlive) { Finish(); break; }
                Collector.Step(run.Clock.FixedStep);
            }
        }
        private void Finish() { Sword.Dispose(); run.CompleteDeath(); }
        public void Dispose() { if (disposed) return; disposed = true; Sword.Dispose(); }
    }
}
