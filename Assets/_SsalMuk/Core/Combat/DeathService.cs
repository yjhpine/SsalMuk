using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    public sealed class DeathService : IDisposable
    {
        private readonly RunModel run;
        private readonly DamageService damage;
        private readonly HashSet<long> queued = new HashSet<long>();
        private bool disposed;
        public DeathService(RunModel run, DamageService damage)
        {
            this.run = run ?? throw new ArgumentNullException(nameof(run)); this.damage = damage ?? throw new ArgumentNullException(nameof(damage));
            damage.Accepted += OnHit;
        }
        private void OnHit(CombatEvent hit)
        {
            if (hit.Killed && run.World.Units.TryGet(hit.Request.TargetId, out var target) && target.Kind != UnitKind.Player) queued.Add(target.Id);
        }
        public void Flush()
        {
            if (disposed) return;
            var deaths = new List<long>(queued); deaths.Sort(); queued.Clear();
            foreach (long id in deaths)
            {
                if (!run.World.Units.TryGet(id, out var enemy) || enemy.IsAlive || enemy.Kind == UnitKind.Player) continue;
                long nextKills = checked(run.Kills + 1);
                if (enemy.Definition.ExperienceReward.Sign > 0) run.World.AddExperience(enemy.Position, enemy.Definition.ExperienceReward);
                run.World.Units.Remove(id); run.Kills = nextKills;
            }
        }
        public void Dispose() { if (disposed) return; disposed = true; damage.Accepted -= OnHit; queued.Clear(); }
    }
}
