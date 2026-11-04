using System;
using System.Collections.Generic;
namespace SsalMuk.Core
{
    public enum ItemEffectKind { Lightning, Explosion, Healing, Shield }
    public sealed class ItemEffectVisual
    {
        public long Id { get; }
        public ItemEffectKind Kind { get; }
        public WorldPosition Position { get; }
        public IReadOnlyList<WorldPosition> Points { get; }
        public double Radius { get; }
        public double StartedAt { get; }
        public const double Duration = .25;
        internal ItemEffectVisual(long id, ItemEffectKind kind, WorldPosition position, double radius, double time, List<WorldPosition> points = null)
        { Id = id; Kind = kind; Position = position; Radius = radius; StartedAt = time; Points = points?.AsReadOnly(); }
    }
    public interface IItemCombatReadModel { IReadOnlyList<ItemEffectVisual> ItemVisuals { get; } }

    public sealed class ItemEffectSystem : IDisposable
    {
        private readonly RunModel run;
        private readonly DamageService damage;
        private readonly List<(CombatEvent hit, WorldPosition position)> hits = new List<(CombatEvent, WorldPosition)>();
        private readonly List<long> candidates = new List<long>();
        private readonly HashSet<long> linked = new HashSet<long>();
        private readonly List<ItemEffectVisual> visuals = new List<ItemEffectVisual>();
        private double lastLife = double.NegativeInfinity, lastChain = double.NegativeInfinity, lastExplosion = double.NegativeInfinity;
        private double shieldCapacity, lastRecharge;
        private long lastVisualId;
        private bool disposed;
        public IReadOnlyList<ItemEffectVisual> Visuals { get; }
        private bool Active => !disposed && run.Phase == RunPhase.Running && !run.IsPaused && run.Player != null && run.Player.IsAlive;
        public ItemEffectSystem(RunModel run, DamageService damage)
        {
            this.run = run; this.damage = damage; Visuals = visuals.AsReadOnly();
            damage.Accepted += OnHit; run.Inventory.Changed += InventoryChanged; InventoryChanged();
        }
        private void InventoryChanged()
        {
            if (run.Player == null) return;
            double capacity = run.Inventory.Bonuses.ShieldCapacity;
            if (capacity == 0) { run.Player.Shield = 0; lastRecharge = run.Clock.ElapsedSeconds; }
            else if (shieldCapacity == 0) { run.Player.Shield = capacity; lastRecharge = run.Clock.ElapsedSeconds; }
            else run.Player.Shield = Math.Min(run.Player.Shield, capacity);
            shieldCapacity = capacity;
        }
        public void Tick()
        {
            if (!Active) return;
            double now = run.Clock.ElapsedSeconds;
            for (int i = visuals.Count - 1; i >= 0; i--) if (now - visuals[i].StartedAt >= ItemEffectVisual.Duration) visuals.RemoveAt(i);
            if (shieldCapacity > 0 && now - lastRecharge + 1e-9 >= run.Inventory.Bonuses.ShieldInterval)
            { run.Player.Shield = shieldCapacity; lastRecharge = now; Visual(ItemEffectKind.Shield, run.Player.Position, .65); }
        }
        private void OnHit(CombatEvent hit)
        {
            if (!Active || hit.Request.SourceId != run.Player.Id || hit.Request.TargetId == run.Player.Id ||
                hit.Request.Origin != DamageOrigin.Weapon || hit.ActualHealthDamage <= 0 || !run.World.Units.TryGet(hit.Request.TargetId, out var enemy)) return;
            var b = run.Inventory.Bonuses;
            if (b.LifeRatio > 0 || b.ChainDamage > 0 || b.ExplosionDamage > 0) hits.Add((hit, enemy.Position));
        }
        public void ProcessHits()
        {
            if (!Active) return;
            var b = run.Inventory.Bonuses;
            foreach (var entry in hits)
            {
                var hit = entry.hit; double time = hit.Time;
                if (b.LifeRatio > 0 && time - lastLife + 1e-9 >= .5)
                {
                    double heal = Math.Min(b.LifeCap, hit.ActualHealthDamage * b.LifeRatio);
                    run.Player.Health = Math.Min(run.Player.Definition.MaxHealth, run.Player.Health + heal); lastLife = time;
                    Visual(ItemEffectKind.Healing, run.Player.Position, .45);
                }
                if (b.ChainDamage > 0 && time - lastChain + 1e-9 >= 1)
                { lastChain = time; Chain(entry.position, hit, b); }
                if (b.ExplosionDamage > 0 && hit.Killed && time - lastExplosion + 1e-9 >= .5)
                { lastExplosion = time; Explode(entry.position, hit, b); }
            }
            hits.Clear();
        }
        private void Chain(WorldPosition origin, CombatEvent trigger, ItemBonuses bonus)
        {
            linked.Clear(); linked.Add(trigger.Request.TargetId); var position = origin;
            var points = new List<WorldPosition> { origin }; long attack = run.AllocateAttackId();
            for (int link = 0; link < bonus.ChainCount; link++)
            {
                run.World.Query.QueryCircle(position, bonus.ChainRange, candidates);
                UnitModel best = null; double nearest = double.PositiveInfinity;
                foreach (long id in candidates)
                {
                    if (linked.Contains(id) || !run.World.Units.TryGet(id, out var enemy) || !enemy.IsAlive || enemy.Kind == UnitKind.Player) continue;
                    double distance = position.DistanceTo(enemy.Position);
                    if (distance <= bonus.ChainRange && (distance < nearest || distance == nearest && (best == null || id < best.Id))) { nearest = distance; best = enemy; }
                }
                if (best == null) break;
                linked.Add(best.Id); Apply(attack, best, trigger.Request.Amount * bonus.ChainDamage, position);
                position = best.Position; points.Add(position);
            }
            damage.CompleteAttack(attack);
            if (points.Count > 1) Visual(ItemEffectKind.Lightning, origin, 0, points);
        }
        private void Explode(WorldPosition position, CombatEvent trigger, ItemBonuses bonus)
        {
            long attack = run.AllocateAttackId(); run.World.Query.QueryCircle(position, bonus.ExplosionRadius, candidates);
            foreach (long id in candidates)
                if (run.World.Units.TryGet(id, out var enemy) && enemy.IsAlive && enemy.Kind != UnitKind.Player)
                    Apply(attack, enemy, trigger.Request.Amount * bonus.ExplosionDamage, position);
            damage.CompleteAttack(attack); Visual(ItemEffectKind.Explosion, position, bonus.ExplosionRadius);
        }
        private void Apply(long attack, UnitModel enemy, double amount, WorldPosition origin)
            => damage.TryApply(new DamageRequest(new HitKey(run.Id, attack, 0, 0), run.Player.Id, enemy.Id, amount,
                origin.DisplacementTo(enemy.Position).Normalized, origin: DamageOrigin.Item), run.Clock.ElapsedSeconds);
        private void Visual(ItemEffectKind kind, WorldPosition position, double radius, List<WorldPosition> points = null)
            => visuals.Add(new ItemEffectVisual(checked(++lastVisualId), kind, position, radius, run.Clock.ElapsedSeconds, points));
        public void Dispose()
        { if (disposed) return; disposed = true; damage.Accepted -= OnHit; run.Inventory.Changed -= InventoryChanged; hits.Clear(); visuals.Clear(); }
    }
}
