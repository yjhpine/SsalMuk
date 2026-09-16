using SsalMuk.Core;
using UnityEngine;

namespace SsalMuk.Unity
{
    public sealed class UnitView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private SpriteRenderer shadow;
        private SpriteRenderer healthBackground, healthFill;
        public long UnitId { get; private set; }
        public double DisplayedHealthFraction { get; private set; }
        public void Configure(SpriteRenderer bodyRenderer, SpriteRenderer shadowRenderer) { body = bodyRenderer; shadow = shadowRenderer; }
        public void Show(UnitModel unit, DVec2 relative, GameCatalog catalog)
        {
            long id = unit.Id; UnitKind kind = unit.Kind; double radius = unit.BodyRadius;
            UnitId = id; transform.localPosition = new Vector3((float)relative.X, (float)relative.Y, 0);
            if (body == null) throw new System.InvalidOperationException("Unit sprite renderer is missing.");
            body.sprite = catalog.GetSprite(kind); body.sharedMaterial = catalog.WorldMaterial;
            body.color = kind == UnitKind.Player ? new Color32(143, 246, 210, 255) : kind == UnitKind.Air ? new Color32(182, 147, 246, 255) :
                kind == UnitKind.Boss ? new Color32(246, 178, 97, 255) : new Color32(221, 105, 108, 255);
            body.transform.localPosition = new Vector3(0, (float)-radius, 0);
            body.transform.localScale = new Vector3((float)(radius * 2.3), (float)(radius * 2.6), 1);
            body.sortingOrder = 100 - Mathf.RoundToInt((float)relative.Y * 10) + (kind == UnitKind.Air ? 300 : 0);
            if (shadow != null)
            {
                shadow.sprite = body.sprite; shadow.sharedMaterial = catalog.WorldMaterial; shadow.color = new Color(0, 0, 0, 0.25f);
                shadow.transform.localPosition = new Vector3(0, (float)-radius, 0);
                shadow.transform.localScale = new Vector3((float)(radius * 2.6), (float)(radius * 0.8), 1); shadow.sortingOrder = body.sortingOrder - 1;
            }
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
            float width = (float)(radius * 2.6); float fraction = (float)DisplayedHealthFraction;
            healthBackground.transform.localPosition = new Vector3(0, (float)(radius * 1.8), 0);
            healthFill.transform.localPosition = new Vector3(width * (fraction - 1) / 2, (float)(radius * 1.8), 0);
            healthBackground.transform.localScale = new Vector3(width, 0.07f, 1);
            healthFill.transform.localScale = new Vector3(width * fraction, 0.045f, 1);
            healthBackground.sortingOrder = body.sortingOrder + 1; healthFill.sortingOrder = body.sortingOrder + 2;
        }
    }
}
