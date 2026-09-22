using SsalMuk.Core;
using UnityEngine;

namespace SsalMuk.Unity
{
    public sealed class UnitView : PooledView
    {
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private Transform walkPivot, hitScaleRoot;
        private SpriteRenderer healthBackground, healthFill, chargeWarning;
        private readonly WalkAnimator walk = new WalkAnimator();
        private readonly HitFeedback hit = new HitFeedback();
        public long UnitId { get; private set; }
        public double DisplayedHealthFraction { get; private set; }
        public SpriteRenderer Body => body;
        public Transform WalkPivot => walkPivot;
        public Transform HitScaleRoot => hitScaleRoot;
        public void Configure(SpriteRenderer bodyRenderer)
        { body = bodyRenderer; PrepareHierarchy(); }
        public void PrepareHierarchy()
        {
            if (walkPivot == null)
            { var go = new GameObject("WalkPivot"); go.transform.SetParent(transform, false); walkPivot = go.transform; }
            if (hitScaleRoot == null)
            { var go = new GameObject("HitScaleRoot"); go.transform.SetParent(walkPivot, false); hitScaleRoot = go.transform; }
            if (body != null) { body.transform.SetParent(hitScaleRoot, false); body.name = "Sprite"; body.transform.localPosition = Vector3.zero; }
        }
        public bool TryApplyHit(LeaseToken token, long sequence, double acceptedAt) => Accepts(token) && hit.Observe(sequence, acceptedAt);
        public void Show(UnitModel unit, DVec2 relative, GameCatalog catalog, double now = 0, double dt = 0.02)
        {
            if (body == null) throw new System.InvalidOperationException("Unit sprite renderer is missing.");
            if (walkPivot == null || hitScaleRoot == null) PrepareHierarchy();
            UnitId = unit.Id; var kind = unit.Kind; double radius = unit.Definition.VisualFootOffset;
            transform.localPosition = WorldRenderOrigin.ToVector(relative);
            var art = catalog.Visuals.Unit(kind); body.sprite = art.Sprite; body.sharedMaterial = catalog.WorldMaterial;
            hit.Observe(unit.HitSequence, unit.LastHitAt); hit.Sample(now, catalog.Visuals);
            if (kind == UnitKind.Player) walk.Step(unit.MoveIntent, dt, catalog.Visuals);
            else walk.Reset();
            walkPivot.localPosition = new Vector3(0, (float)(-radius + walk.Pose.Height), 0);
            walkPivot.localRotation = Quaternion.Euler(0, 0, (float)walk.Pose.RotationDegrees);
            hitScaleRoot.localScale = Vector3.one * hit.Scale; body.color = hit.Color;
            body.transform.localPosition = Vector3.zero; body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = Vector3.one * (art.Height / body.sprite.bounds.size.y);
            if (System.Math.Abs(unit.MoveIntent.X) > 0.01) body.flipX = unit.MoveIntent.X < 0;
            int order = 1000 - Mathf.RoundToInt((float)(relative.Y - radius) * 30) + (kind == UnitKind.Air ? 5000 : 0);
            body.sortingOrder = order;
            if (healthBackground == null)
            {
                var back = new GameObject("HealthBackground", typeof(SpriteRenderer)); back.transform.SetParent(transform, false);
                var fill = new GameObject("HealthFill", typeof(SpriteRenderer)); fill.transform.SetParent(transform, false);
                healthBackground = back.GetComponent<SpriteRenderer>(); healthFill = fill.GetComponent<SpriteRenderer>();
                healthBackground.sprite = healthFill.sprite = catalog.SolidSprite;
                healthBackground.sharedMaterial = healthFill.sharedMaterial = catalog.WorldMaterial;
                healthBackground.color = new Color(0, 0, 0, 0.7f); healthFill.color = new Color32(240, 88, 86, 255);
            }
            DisplayedHealthFraction = unit.Health / unit.Definition.MaxHealth;
            healthBackground.enabled = healthFill.enabled = kind != UnitKind.Player && DisplayedHealthFraction < 1;
            float width = (float)(radius * 2.6), fraction = (float)DisplayedHealthFraction, y = art.Height - (float)radius + 0.12f;
            healthBackground.transform.localPosition = new Vector3(0, y, 0);
            healthFill.transform.localPosition = new Vector3(width * (fraction - 1) / 2, y, 0);
            healthBackground.transform.localScale = new Vector3(width, 0.07f, 1);
            healthFill.transform.localScale = new Vector3(width * fraction, 0.045f, 1);
            healthBackground.sortingOrder = order + 1; healthFill.sortingOrder = order + 2;
            ShowChargeWarning(unit, catalog);
        }
        private void ShowChargeWarning(UnitModel unit, GameCatalog catalog)
        {
            var charge = (unit as GroundEnemyModel)?.Charge;
            bool visible = charge != null && charge.Phase == EnemyState.Telegraph && unit.IsAlive && !unit.Knockback.IsActive;
            if (!visible) { if (chargeWarning != null) chargeWarning.enabled = false; return; }
            if (chargeWarning == null)
            {
                var go = new GameObject("BossChargeWarning", typeof(SpriteRenderer)); go.transform.SetParent(transform, false);
                chargeWarning = go.GetComponent<SpriteRenderer>(); chargeWarning.sprite = catalog.SolidSprite;
                chargeWarning.sharedMaterial = catalog.WorldMaterial; chargeWarning.color = new Color(1, .035f, .025f, .48f);
                chargeWarning.sortingOrder = -100;
            }
            chargeWarning.enabled = true;
            var center = unit.Position.DisplacementTo(charge.Origin.Offset(charge.Direction * (charge.Length / 2)));
            chargeWarning.transform.localPosition = WorldRenderOrigin.ToVector(center);
            chargeWarning.transform.localRotation = Quaternion.Euler(0, 0, (float)(System.Math.Atan2(charge.Direction.Y, charge.Direction.X) * 180 / System.Math.PI));
            var size = chargeWarning.sprite.bounds.size;
            chargeWarning.transform.localScale = new Vector3((float)(charge.Length / size.x), (float)(unit.BodyRadius * 2 / size.y), 1);
        }
        public override void ResetVisuals()
        {
            base.ResetVisuals(); UnitId = 0; DisplayedHealthFraction = 0; walk.Reset(); hit.Reset();
            if (walkPivot != null) { walkPivot.localPosition = Vector3.zero; walkPivot.localRotation = Quaternion.identity; }
            if (hitScaleRoot != null) hitScaleRoot.localScale = Vector3.one;
            if (body != null) { body.color = Color.white; body.flipX = false; body.transform.localRotation = Quaternion.identity; body.transform.localScale = Vector3.one; }
            if (healthBackground != null) healthBackground.enabled = healthFill.enabled = false;
            if (chargeWarning != null) chargeWarning.enabled = false;
        }
    }
}
