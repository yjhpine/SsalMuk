using System;
using System.Linq;
using SsalMuk.Core;
using UnityEngine;
namespace SsalMuk.Unity
{
    [CreateAssetMenu(menuName = "SsalMuk/Visual Catalog")]
    public sealed class VisualCatalog : ScriptableObject
    {
        [SerializeField] private UnitArt[] units = Array.Empty<UnitArt>();
        [SerializeField] private WeaponArt[] weapons = Array.Empty<WeaponArt>();
        [SerializeField] private Sprite[] projectileFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] explosionFrames = Array.Empty<Sprite>();
        [SerializeField] private float projectileFrameSeconds = 0.08f;
        [SerializeField] private float projectileSourceAxisDegrees = 180;
        [SerializeField] private float walkAngle = 8, walkHeight = 0.08f, walkCycles = 1.8f;
        [SerializeField] private float hitRedSeconds = 0.1f, hitPopSeconds = 0.12f, hitPopScale = 1.15f;
        [SerializeField] private Color[] experienceColors = { new Color(0.2f, 1, 0.38f), new Color(0.2f, 0.58f, 1), new Color(1, 0.22f, 0.16f) };
        public float WalkAngle => walkAngle;
        public float WalkHeight => walkHeight;
        public float WalkCycles => walkCycles;
        public float HitRedSeconds => hitRedSeconds;
        public float HitPopSeconds => hitPopSeconds;
        public float HitPopScale => hitPopScale;
        public float ProjectileFrameSeconds => projectileFrameSeconds;
        public float ProjectileSourceAxisDegrees => projectileSourceAxisDegrees;
        public Sprite[] ProjectileFrames => projectileFrames;
        public Sprite[] ExplosionFrames => explosionFrames;
        public UnitArt Unit(UnitKind kind) => units.First(x => x.Kind == kind);
        public WeaponArt Weapon(WeaponKind kind) => weapons.First(x => x.Kind == kind);
        public Color ExperienceColor(ExperienceTier tier) => experienceColors[(int)tier];
        public float ExperienceHaloRadius(ExperienceTier tier) => 0.27f + 0.03f * (int)tier;
        public void Configure(UnitArt[] unitArt, WeaponArt[] weaponArt, Sprite[] projectiles, Sprite[] explosions)
        { units = unitArt; weapons = weaponArt; projectileFrames = projectiles; explosionFrames = explosions; Validate(); }
        public void Validate()
        {
            foreach (UnitKind kind in Enum.GetValues(typeof(UnitKind)))
                if (units.Count(x => x != null && x.Kind == kind && x.Sprite != null && x.Height > 0) != 1) throw new ArgumentException("Missing or duplicate unit art: " + kind);
            foreach (WeaponKind kind in Enum.GetValues(typeof(WeaponKind)))
                if (weapons.Count(x => x != null && x.Kind == kind && x.Sprite != null && x.Effect != null) != 1) throw new ArgumentException("Missing or duplicate weapon art: " + kind);
            if (projectileFrames.Length != 4 || explosionFrames.Length != 50 || projectileFrames.Any(x => x == null) || explosionFrames.Any(x => x == null))
                throw new ArgumentException("The licensed projectile and explosion frames are incomplete.");
            if (experienceColors.Length != 3 || walkCycles <= 0 || hitRedSeconds <= 0 || hitPopSeconds <= 0 || hitPopScale < 1) throw new ArgumentException("Invalid visual timing or colors.");
        }
        [Serializable]
        public sealed class UnitArt
        {
            public UnitKind Kind; public Sprite Sprite; public float Height;
            public UnitArt(UnitKind kind, Sprite sprite, float height) { Kind = kind; Sprite = sprite; Height = height; }
        }
        [Serializable]
        public sealed class WeaponArt
        {
            public WeaponKind Kind; public Sprite Sprite; public Sprite Effect; public float EffectAxisDegrees;
            public float ContactFraction;
            public WeaponArt(WeaponKind kind, Sprite sprite, Sprite effect, float effectAxis, float contactFraction = 1)
            { Kind = kind; Sprite = sprite; Effect = effect; EffectAxisDegrees = effectAxis; ContactFraction = contactFraction; }
        }
    }
}
