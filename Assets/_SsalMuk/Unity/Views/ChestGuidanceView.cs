using SsalMuk.Core;
using UnityEngine;

namespace SsalMuk.Unity
{
    public sealed class ChestGuidanceView : MonoBehaviour
    {
        private Canvas canvas;
        private RectTransform marker, arrow;
        private UnityEngine.UI.Text label;
        private Camera cameraComponent;
        public bool IsVisible => marker != null && marker.gameObject.activeSelf;
        public Vector2 MarkerPosition => marker.anchoredPosition;
        public void Initialize(Font font)
        {
            canvas = UiFactory.Canvas(transform, "ChestGuidanceCanvas", 30);
            marker = UiFactory.Rect(canvas.transform, "ChestMarker", new Vector2(100, 64), new Vector2(.5f, .5f), Vector2.zero);
            arrow = UiFactory.Rect(marker, "Arrow", new Vector2(30, 30), new Vector2(.5f, .5f), Vector2.zero);
            var graphic = arrow.gameObject.AddComponent<ChestArrowGraphic>(); graphic.color = new Color32(255, 213, 103, 255); graphic.raycastTarget = false;
            label = UiFactory.Label(marker, "ChestLabel", font, "상자", 17, graphic.color, new Vector2(100, 28), new Vector2(.5f, .5f), new Vector2(0, -28));
            marker.gameObject.SetActive(false);
        }
        public void Show(DVec2? relative)
        {
            if (cameraComponent == null) cameraComponent = Camera.main;
            if (!relative.HasValue || cameraComponent == null) { marker.gameObject.SetActive(false); return; }
            var viewport = cameraComponent.WorldToViewportPoint(WorldRenderOrigin.ToVector(relative.Value));
            if (viewport.x >= .04f && viewport.x <= .96f && viewport.y >= .04f && viewport.y <= .96f)
            { marker.gameObject.SetActive(false); return; }
            var size = ((RectTransform)canvas.transform).rect.size;
            var delta = new Vector2((viewport.x - .5f) * size.x, (viewport.y - .5f) * size.y);
            float halfX = Mathf.Max(1, size.x / 2 - 66), halfY = Mathf.Max(1, size.y / 2 - (delta.y < 0 ? 245 : 90));
            float scale = Mathf.Min(halfX / Mathf.Max(.001f, Mathf.Abs(delta.x)), halfY / Mathf.Max(.001f, Mathf.Abs(delta.y)));
            marker.anchoredPosition = delta * Mathf.Min(1, scale);
            arrow.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            label.text = "상자 " + relative.Value.Length.ToString("0") + "m";
            marker.gameObject.SetActive(true);
        }
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ChestArrowGraphic : UnityEngine.UI.Graphic
    {
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper helper)
        {
            helper.Clear(); var r = rectTransform.rect;
            helper.AddVert(new Vector3(r.xMax, 0), color, Vector2.zero);
            helper.AddVert(new Vector3(r.xMin, r.yMax), color, Vector2.zero);
            helper.AddVert(new Vector3(r.xMin * .3f, 0), color, Vector2.zero);
            helper.AddVert(new Vector3(r.xMin, r.yMin), color, Vector2.zero);
            helper.AddTriangle(0, 2, 1); helper.AddTriangle(0, 3, 2);
        }
    }
}
