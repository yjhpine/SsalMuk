using SsalMuk.Core;
using UnityEngine;

namespace SsalMuk.Unity
{
    public sealed class UnitView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private SpriteRenderer shadow;
        public long UnitId { get; private set; }
        public void Configure(SpriteRenderer bodyRenderer, SpriteRenderer shadowRenderer) { body = bodyRenderer; shadow = shadowRenderer; }
        public void Show(long id, UnitKind kind, DVec2 relative, double radius, GameCatalog catalog)
        {
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
        }
    }
}
