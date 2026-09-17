using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SsalMuk.Core
{
    public sealed class RunSimulation : IDisposable, ICombatReadModel
    {
        private readonly RunModel run;
        private readonly MovementSystem movement;
        private readonly LowAiController ai;
        private readonly DeathService death;
        private readonly ContactDamageSystem contact;
        private readonly DamageService damage;
        public event Action<CombatEvent> DamageAccepted;
        private bool disposed;
        private WorldRect? viewBounds;
        public SpawnDirector Spawns { get; }
        public NavigationService Navigation => movement.Navigation;
        public IEnumerable<AttackShapeSnapshot> AttackShapes
        {
            get { foreach (var weapon in Weapons.Values) foreach (var attack in weapon.ActiveAttacks) yield return new AttackShapeSnapshot(attack, run.Definitions.GetWeapon(attack.Kind)); }
        }
        public IReadOnlyList<ProjectileModel> Projectiles => Weapons[WeaponKind.Fireball].Projectiles.Active;
        public IReadOnlyList<ExplosionSnapshot> Explosions => Weapons[WeaponKind.Fireball].Projectiles.Explosions;
        public WeaponRuntime Sword { get; }
        public IReadOnlyDictionary<WeaponKind, WeaponRuntime> Weapons { get; }
        public ProgressionService Progression { get; }
        public ExperienceCollector Collector { get; }
        public RunSimulation(RunModel run, MovementSystem movement, LowAiController ai, DamageService damage, DeathService death,
            ContactDamageSystem contact, bool scheduledSpawns = false, SpawnSettings spawnSettings = null)
        {
            this.run = run; this.movement = movement; this.ai = ai; this.death = death; this.contact = contact; this.damage = damage;
            Progression = new ProgressionService(run); Collector = new ExperienceCollector(run, movement, Progression);
            var weapons = new Dictionary<WeaponKind, WeaponRuntime>();
            foreach (WeaponKind kind in Enum.GetValues(typeof(WeaponKind))) weapons.Add(kind, new WeaponRuntime(run, kind, movement, damage));
            Weapons = new ReadOnlyDictionary<WeaponKind, WeaponRuntime>(weapons); Sword = weapons[WeaponKind.Sword];
            damage.Accepted += ForwardHit;
            if (scheduledSpawns) Spawns = new SpawnDirector(run, spawnSettings);
            run.Combat = this;
        }
        public void SetViewBounds(WorldRect bounds) => viewBounds = bounds;
        public void Step(double dt)
        {
            if (disposed || run.Phase != RunPhase.Running) return;
            double count = dt / run.Clock.FixedStep;
            if (dt < 0 || double.IsNaN(dt) || double.IsInfinity(dt) || count > int.MaxValue || Math.Abs(count - Math.Round(count)) > 1e-8)
                throw new ArgumentOutOfRangeException(nameof(dt), "Simulation uses whole fixed steps.");
            for (int i = 0; i < (int)Math.Round(count); i++)
            {
                if (!run.Player.IsAlive) { Finish(); break; }
                run.Rewards.Step();
                double from = run.Clock.ElapsedSeconds; run.Clock.Advance(); double to = run.Clock.ElapsedSeconds;
                Spawns?.Tick(to, viewBounds ?? new WorldRect(run.Player.Position, 16, 9));
                if (ai != null) { ai.Tick(run.Clock.FixedStep); movement.SetMoveIntent(run.Player.Id, run.Player.MoveIntent); }
                movement.Step(run.Clock.FixedStep);
                foreach (var kind in run.Player.Weapons.Kinds) Weapons[kind].Tick(from, to);
                death.Flush(); contact.Step(from, to);
                if (!run.Player.IsAlive) { Finish(); break; }
                Collector.Step(run.Clock.FixedStep);
                run.Rewards.RefreshOffer();
            }
        }
        private void ForwardHit(CombatEvent hit) => DamageAccepted?.Invoke(hit);
        private void Finish() { Dispose(); run.CompleteDeath(); }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            damage.Accepted -= ForwardHit; DamageAccepted = null;
            Spawns?.Dispose();
            if (ReferenceEquals(run.Combat, this)) run.Combat = null;
            foreach (var weapon in Weapons.Values) weapon.Dispose();
        }
    }
}
