using System;
using SsalMuk.Core;
using UnityEngine;
namespace SsalMuk.Unity
{
    public sealed class ProjectileView : PooledView
    {
        private GameCatalog catalog;
        private SpriteRenderer sprite;
        public long AttackId { get; private set; }
        public void Initialize(GameCatalog gameCatalog)
        { catalog = gameCatalog; sprite = gameObject.AddComponent<SpriteRenderer>(); sprite.sharedMaterial = catalog.WorldMaterial; sprite.sortingOrder = 2300; }
        public void Show(ProjectileModel projectile, DVec2 relative, double time)
        {
            AttackId = projectile.Key.AttackId; transform.localPosition = WorldRenderOrigin.ToVector(relative);
            transform.localRotation = Quaternion.Euler(0, 0, (float)(Math.Atan2(projectile.Direction.Y, projectile.Direction.X) * 180 / Math.PI) - catalog.Visuals.ProjectileSourceAxisDegrees);
            var frames = catalog.Visuals.ProjectileFrames;
            int index = (int)(Math.Max(0, time - projectile.SpawnedAt) / catalog.Visuals.ProjectileFrameSeconds) % frames.Length;
            sprite.sprite = frames[index]; sprite.color = Color.white; transform.localScale = Vector3.one * 0.65f;
        }
        public override void ResetVisuals() { base.ResetVisuals(); AttackId = 0; if (sprite != null) { sprite.sprite = null; sprite.color = Color.white; } }
    }
}
