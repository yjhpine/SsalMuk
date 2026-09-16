using System;
using SsalMuk.Core;
using UnityEngine;
namespace SsalMuk.Unity
{
    public sealed class AttackView : PooledView
    {
        private GameCatalog catalog;
        private SpriteRenderer weapon, effect;
        private ShapeRenderer shape;
        public AttackShapeSnapshot Snapshot { get; private set; }
        public double DisplayedRange { get; private set; }
        public bool IsExplosion { get; private set; }
        public SpriteRenderer WeaponSprite => weapon;
        public Bounds ShapeBounds => shape.LocalBounds;
        public void Initialize(GameCatalog gameCatalog)
        {
            catalog = gameCatalog; weapon = Sprite("Weapon", 2100); effect = Sprite("Effect", 2050);
            shape = ShapeRenderer.Create(transform, "AttackRange", catalog.WorldMaterial, 2000);
        }
        private SpriteRenderer Sprite(string name, int order)
        {
            var go = new GameObject(name, typeof(SpriteRenderer)); go.transform.SetParent(transform, false);
            var renderer = go.GetComponent<SpriteRenderer>(); renderer.sharedMaterial = catalog.WorldMaterial; renderer.sortingOrder = order; return renderer;
        }
        public void Show(AttackShapeSnapshot snapshot, DVec2 relative)
        {
            Snapshot = snapshot; DisplayedRange = snapshot.Range; IsExplosion = false;
            var art = catalog.Visuals.Weapon(snapshot.Kind); float range = WorldRenderOrigin.ToFloat(snapshot.Range);
            double baseAngle = Math.Atan2(snapshot.Direction.Y, snapshot.Direction.X);
            transform.localPosition = WorldRenderOrigin.ToVector(relative); transform.localRotation = Quaternion.Euler(0, 0, (float)(baseAngle * 180 / Math.PI));
            weapon.enabled = true; weapon.sprite = art.Sprite; weapon.color = Color.white;
            weapon.transform.localRotation = Quaternion.Euler(0, 0, (float)((snapshot.AngleRadians - baseAngle) * 180 / Math.PI));
            weapon.transform.localPosition = Vector3.zero;
            float length = snapshot.Kind == WeaponKind.Fireball ? 0.9f : range / art.ContactFraction;
            if (snapshot.Kind == WeaponKind.Spear)
            { length = Math.Min(range, 1.35f); weapon.transform.localPosition = new Vector3(range * (float)snapshot.Progress - length, 0, 0); }
            float scale = length / weapon.sprite.bounds.size.x;
            float yScale = snapshot.Kind == WeaponKind.Axe ? Mathf.Min(scale, 0.6f / weapon.sprite.bounds.size.y) : scale;
            weapon.transform.localScale = new Vector3(scale, yScale, 1);
            effect.sprite = art.Effect; effect.enabled = snapshot.Kind != WeaponKind.Fireball;
            effect.color = new Color(0.82f, 0.98f, 1, 0.46f);
            effect.transform.localPosition = Vector3.zero;
            effect.transform.localRotation = Quaternion.Euler(0, 0, (float)((snapshot.AngleRadians - baseAngle) * 180 / Math.PI) - art.EffectAxisDegrees);
            if (snapshot.Kind == WeaponKind.Sword)
            {
                shape.Ring(0, range, -Math.PI / 6, -Math.PI / 6 + snapshot.Progress * Math.PI / 3, new Color(0.7f, 0.92f, 1, 0.13f));
                effect.transform.localScale = new Vector3(range / effect.sprite.bounds.size.x, range / effect.sprite.bounds.size.y, 1);
            }
            else if (snapshot.Kind == WeaponKind.Spear)
            {
                float reach = range * (float)snapshot.Progress;
                shape.Capsule(reach, snapshot.Width / 2, new Color(0.7f, 0.92f, 1, 0.15f));
                effect.transform.localPosition = new Vector3(reach / 2, 0, 0);
                effect.transform.localRotation = Quaternion.identity;
                effect.transform.localScale = new Vector3(reach / effect.sprite.bounds.size.x, (float)snapshot.Width / effect.sprite.bounds.size.y, 1);
            }
            else if (snapshot.Kind == WeaponKind.Axe)
            {
                double start = CopyLayout.Phase(snapshot.Key.Copy);
                shape.Ring(Math.Max(0, range - snapshot.Width), range + snapshot.Width, start, start + snapshot.Progress * Math.PI * 2, new Color(0.82f, 0.92f, 1, 0.14f));
                effect.transform.localScale = new Vector3(range * 2 / effect.sprite.bounds.size.x, range * 2 / effect.sprite.bounds.size.y, 1);
            }
            else shape.Clear();
        }
        public void ShowExplosion(ExplosionSnapshot explosion, DVec2 relative, double time, double lifetime)
        {
            IsExplosion = true; DisplayedRange = explosion.Radius;
            transform.localPosition = WorldRenderOrigin.ToVector(relative); transform.localRotation = Quaternion.identity;
            weapon.enabled = false; effect.enabled = true;
            double progress = Math.Max(0, Math.Min(1, (time - explosion.Time) / lifetime));
            var frames = catalog.Visuals.ExplosionFrames; effect.sprite = frames[Math.Min(frames.Length - 1, (int)(progress * frames.Length))];
            effect.color = new Color(1, 1, 1, (float)(1 - progress * 0.5)); effect.transform.localPosition = Vector3.zero; effect.transform.localRotation = Quaternion.identity;
            float diameter = WorldRenderOrigin.ToFloat(explosion.Radius * 2);
            effect.transform.localScale = new Vector3(diameter / effect.sprite.bounds.size.x, diameter / effect.sprite.bounds.size.y, 1);
            shape.Disc(explosion.Radius, new Color(1, 0.45f, 0.15f, (float)((1 - progress) * 0.18)));
        }
        public override void ResetVisuals()
        {
            base.ResetVisuals(); Snapshot = default; DisplayedRange = 0; IsExplosion = false;
            if (weapon != null) { weapon.enabled = false; weapon.color = Color.white; weapon.sprite = null; }
            if (effect != null) { effect.enabled = false; effect.color = Color.white; effect.sprite = null; }
            if (shape != null) shape.Clear();
        }
    }
}
