using System;
using System.Collections.Generic;
using SsalMuk.Core;
using SsalMuk.Presentation;
using UnityEngine;

namespace SsalMuk.Unity
{
    public sealed class WorldView : MonoBehaviour, ICombatWorldView
    {
        private GameCatalog catalog;
        private ViewLayer<UnitView> units;
        private ViewLayer<ExperienceView> experience;
        private ViewLayer<AttackView> attacks, explosions;
        private ViewLayer<ProjectileView> projectiles;
        private readonly Dictionary<ChunkCoord, TerrainView> terrain = new Dictionary<ChunkCoord, TerrainView>();
        private readonly HashSet<ChunkCoord> seenTerrain = new HashSet<ChunkCoord>();
        private readonly List<ChunkCoord> terrainReturns = new List<ChunkCoord>();
        private Guid runId;
        private double frameTime, frameDelta;
        private bool hasTime;
        public int VisibleUnitCount => units?.ActiveCount ?? 0;
        public int VisibleExperienceCount => experience?.ActiveCount ?? 0;
        public int VisibleAttackCount => (attacks?.ActiveCount ?? 0) + (explosions?.ActiveCount ?? 0) + (projectiles?.ActiveCount ?? 0);
        public int RetainedViewCount => (units?.RetainedCount ?? 0) + (experience?.RetainedCount ?? 0) + (attacks?.RetainedCount ?? 0) + (explosions?.RetainedCount ?? 0) + (projectiles?.RetainedCount ?? 0);
        public IEnumerable<UnitView> UnitViews => units == null ? Array.Empty<UnitView>() : units.ActiveViews;
        public IEnumerable<ExperienceView> ExperienceViews => experience == null ? Array.Empty<ExperienceView>() : experience.ActiveViews;
        public IEnumerable<AttackView> AttackViews => attacks == null ? Array.Empty<AttackView>() : attacks.ActiveViews;
        public void Initialize(GameCatalog catalog) { this.catalog = catalog; catalog.ValidatePresentation(); }
        public void BeginFrame(Guid nextRun)
        {
            if (runId != nextRun)
            {
                ReleasePools(); runId = nextRun; hasTime = false;
                units = new ViewLayer<UnitView>(runId, () => Instantiate(catalog.UnitViewPrefab, transform).GetComponent<UnitView>());
                experience = new ViewLayer<ExperienceView>(runId, () => { var view = Create<ExperienceView>("Experience"); view.Initialize(catalog); return view; });
                attacks = new ViewLayer<AttackView>(runId, () => { var view = Create<AttackView>("Attack"); view.Initialize(catalog); return view; });
                explosions = new ViewLayer<AttackView>(runId, () => { var view = Create<AttackView>("Explosion"); view.Initialize(catalog); return view; });
                projectiles = new ViewLayer<ProjectileView>(runId, () => { var view = Create<ProjectileView>("Fireball"); view.Initialize(catalog); return view; });
                foreach (var view in terrain.Values) if (view != null) Destroy(view.gameObject);
                terrain.Clear();
            }
            units.BeginFrame(); experience.BeginFrame(); attacks.BeginFrame(); explosions.BeginFrame(); projectiles.BeginFrame(); seenTerrain.Clear();
        }
        private T Create<T>(string name) where T : PooledView
        { var go = new GameObject(name); go.transform.SetParent(transform, false); return go.AddComponent<T>(); }
        public void SetFrameTime(double time) { frameDelta = hasTime ? Math.Max(0, time - frameTime) : 0; frameTime = time; hasTime = true; }
        public void ShowTerrain(ChunkData chunk, DVec2 relativeOrigin)
        {
            seenTerrain.Add(chunk.Coord);
            if (terrain.TryGetValue(chunk.Coord, out var existing) && existing != null && existing.Fingerprint != chunk.Fingerprint)
            { Destroy(existing.gameObject); terrain.Remove(chunk.Coord); }
            if (!terrain.TryGetValue(chunk.Coord, out var view))
            {
                var go = new GameObject("Chunk " + chunk.Coord.X + "," + chunk.Coord.Y); go.transform.SetParent(transform, false);
                view = go.AddComponent<TerrainView>(); view.Initialize(chunk, catalog.WorldMaterial); terrain.Add(chunk.Coord, view);
            }
            view.transform.localPosition = WorldRenderOrigin.ToVector(relativeOrigin);
        }
        public void ShowUnit(UnitModel unit, DVec2 relativePosition) => units.Show(unit.Id).Show(unit, relativePosition, catalog, frameTime, frameDelta);
        public void ShowExperience(ExperienceRecord record, ExperienceTier tier, DVec2 relative, double radius)
            => experience.Show(record.Id).Show(record, tier, WorldRenderOrigin.ToVector(relative), radius);
        public void ShowAttack(AttackShapeSnapshot shape, DVec2 relative) => attacks.Show(shape.Key.AttackId).Show(shape, relative);
        public void ShowProjectile(ProjectileModel projectile, DVec2 relative) => projectiles.Show(projectile.Key.AttackId).Show(projectile, relative, frameTime);
        public void ShowExplosion(ExplosionSnapshot explosion, DVec2 relative, double lifetime) => explosions.Show(explosion.Key.AttackId).ShowExplosion(explosion, relative, frameTime, lifetime);
        public void EndFrame(double elapsedSeconds, int enemyCount)
        {
            units.EndFrame(); experience.EndFrame(); attacks.EndFrame(); explosions.EndFrame(); projectiles.EndFrame();
            terrainReturns.Clear(); foreach (var coord in terrain.Keys) if (!seenTerrain.Contains(coord)) terrainReturns.Add(coord);
            foreach (var coord in terrainReturns) { if (terrain[coord] != null) Destroy(terrain[coord].gameObject); terrain.Remove(coord); }
        }
        private void ReleasePools() { units?.Dispose(); experience?.Dispose(); attacks?.Dispose(); explosions?.Dispose(); projectiles?.Dispose(); }
        private void OnDestroy() => ReleasePools();
    }
}
