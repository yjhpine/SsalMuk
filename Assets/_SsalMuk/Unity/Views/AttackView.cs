using System;
using SsalMuk.Core;
using UnityEngine;
namespace SsalMuk.Unity
{
    public sealed class AttackView : PooledView
    {
        private GameCatalog catalog;
        private SpriteRenderer weapon, effect;
        private Transform effectAnchor;
        private ShapeRenderer shape;
        public AttackShapeSnapshot Snapshot { get; private set; }
        public double DisplayedRange { get; private set; }
        public bool IsExplosion { get; private set; }
        public SpriteRenderer WeaponSprite => weapon;
        public Bounds ShapeBounds => shape.LocalBounds;
        public void Initialize(GameCatalog gameCatalog)
        {
            catalog = gameCatalog; weapon = Sprite("Weapon", 2100, transform);
            effectAnchor = new GameObject("Effect").transform; effectAnchor.SetParent(transform, false);
            effect = Sprite("Artwork", 2050, effectAnchor);
            shape = ShapeRenderer.Create(transform, "AttackRange", catalog.WorldMaterial, 2000);
        }
        private SpriteRenderer Sprite(string name, int order, Transform parent)
        {
            var go = new GameObject(name, typeof(SpriteRenderer)); go.transform.SetParent(parent, false);
            var renderer = go.GetComponent<SpriteRenderer>(); renderer.sharedMaterial = catalog.WorldMaterial; renderer.sortingOrder = order; return renderer;
        }
        public void Show(AttackShapeSnapshot snapshot, DVec2 relative)
        {
            Snapshot = snapshot; DisplayedRange = snapshot.Range; IsExplosion = false;
            var art = catalog.Visuals.Weapon(snapshot.Kind); float range = WorldRenderOrigin.ToFloat(snapshot.Range);
            double baseAngle = Math.Atan2(snapshot.Direction.Y, snapshot.Direction.X);
            transform.localPosition = WorldRenderOrigin.ToVector(relative); transform.localRotation = Quaternion.Euler(0, 0, (float)(baseAngle * 180 / Math.PI));
            weapon.enabled = true; weapon.sprite = art.Sprite; weapon.color = Color.white;
            float localAngle = (float)((snapshot.AngleRadians - baseAngle) * 180 / Math.PI);
            var rotation = Quaternion.Euler(0, 0, localAngle); weapon.transform.localRotation = rotation;
            float reach = snapshot.Kind == WeaponKind.Fireball ? 0.9f : range * (snapshot.Kind == WeaponKind.Spear ? (float)snapshot.Progress : 1);
            // Imported pivots are slightly inside the handle. Measure handle-to-contact length, not pivot-to-edge length.
            float scale = reach / (weapon.sprite.bounds.size.x * art.ContactFraction);
            float yScale = snapshot.Kind == WeaponKind.Axe ? Mathf.Min(scale, 0.6f / weapon.sprite.bounds.size.y) :
                snapshot.Kind == WeaponKind.Spear ? Mathf.Min(scale, (float)snapshot.Width / weapon.sprite.bounds.size.y) : scale;
            weapon.transform.localScale = new Vector3(scale, yScale, 1);
            weapon.transform.localPosition = rotation * new Vector3(-weapon.sprite.bounds.min.x * scale, 0, 0);
            effect.sprite = art.Effect; effect.enabled = snapshot.Kind != WeaponKind.Fireball;
            effect.color = new Color(0.82f, 0.98f, 1, snapshot.Kind == WeaponKind.Sword ? 0.28f : 0.46f);
            effectAnchor.localPosition = rotation * new Vector3(reach, 0, 0);
            effectAnchor.localRotation = Quaternion.Euler(0, 0, localAngle - art.EffectAxisDegrees);
            float effectSize = snapshot.Kind == WeaponKind.Axe ? (float)snapshot.Width * 2 : Mathf.Min(range * 0.35f, 0.7f);
            float effectScale = effectSize / Mathf.Max(effect.sprite.bounds.size.x, effect.sprite.bounds.size.y);
            effectAnchor.localScale = new Vector3(effectScale, effectScale, 1);
            // Keep the imported effect's visible center on the contact point, regardless of its source pivot.
            effect.transform.localPosition = -effect.sprite.bounds.center;
            effect.transform.localRotation = Quaternion.identity; effect.transform.localScale = Vector3.one;
            if (snapshot.Kind == WeaponKind.Sword)
            {
                shape.Ring(0, range, -Math.PI / 6, -Math.PI / 6 + snapshot.Progress * Math.PI / 3, new Color(0.7f, 0.92f, 1, 0.13f));
            }
            else if (snapshot.Kind == WeaponKind.Spear)
            {
                shape.Capsule(reach, snapshot.Width / 2, new Color(0.7f, 0.92f, 1, 0.15f));
            }
            else if (snapshot.Kind == WeaponKind.Axe)
            {
                shape.Ring(Math.Max(0, range - snapshot.Width), range + snapshot.Width, 0, snapshot.Progress * Math.PI * 2, new Color(0.82f, 0.92f, 1, 0.14f));
            }
            else shape.Clear();
        }
        public void ShowExplosion(ExplosionSnapshot explosion, DVec2 relative, double time, double lifetime)
        {
            IsExplosion = true; DisplayedRange = explosion.Radius;
            transform.localPosition = WorldRenderOrigin.ToVector(relative); transform.localRotation = Quaternion.identity;
            weapon.enabled = false; effect.enabled = true;
            ResetTransform(effectAnchor);
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
            if (weapon != null) ResetTransform(weapon.transform);
            if (effect != null) ResetTransform(effect.transform);
            if (effectAnchor != null) ResetTransform(effectAnchor);
            if (shape != null) shape.Clear();
        }
        private static void ResetTransform(Transform target)
        { target.localPosition = Vector3.zero; target.localRotation = Quaternion.identity; target.localScale = Vector3.one; }
    }
}
