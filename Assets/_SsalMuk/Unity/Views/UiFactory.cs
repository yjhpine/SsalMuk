using UnityEngine;
using UnityEngine.UI;

namespace SsalMuk.Unity
{
    internal static class UiFactory
    {
        internal static readonly Color Ink = new Color32(12, 24, 31, 255);
        internal static readonly Color Mint = new Color32(130, 235, 190, 255);
        internal static readonly Color Muted = new Color32(157, 181, 183, 255);
        internal static Canvas Canvas(Transform parent, string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }
        internal static RectTransform Rect(Transform parent, string name, Vector2 size, Vector2 anchor, Vector2 offset)
        {
            var go = new GameObject(name, typeof(RectTransform)); var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = anchor; rect.sizeDelta = size; rect.anchoredPosition = offset;
            return rect;
        }
        internal static Text Label(Transform parent, string name, Font font, string text, int size, Color color, Vector2 dimensions, Vector2 anchor, Vector2 offset, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var rect = Rect(parent, name, dimensions, anchor, offset); var label = rect.gameObject.AddComponent<Text>();
            label.font = font; label.fontSize = size; label.text = text; label.color = color; label.alignment = alignment;
            label.raycastTarget = false; label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }
        internal static Button Button(Transform parent, string name, Font font, string text, Vector2 offset)
        {
            var rect = Rect(parent, name, new Vector2(290, 62), new Vector2(0.5f, 0.5f), offset);
            var background = rect.gameObject.AddComponent<Image>(); background.color = Mint;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = background;
            var colors = button.colors; colors.highlightedColor = new Color(0.9f, 1, 0.95f); colors.pressedColor = new Color(0.65f, 0.85f, 0.75f);
            colors.disabledColor = new Color(0.4f, 0.5f, 0.45f); button.colors = colors;
            Label(rect, "Caption", font, text, 23, Ink, new Vector2(280, 58), new Vector2(0.5f, 0.5f), Vector2.zero);
            return button;
        }
        internal static RectTransform Bar(Transform parent, string name, Vector2 dimensions, Vector2 anchor, Vector2 offset, Color color)
        {
            var back = Rect(parent, name, dimensions, anchor, offset); back.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.14f);
            var fill = Rect(back, "Fill", Vector2.zero, Vector2.zero, Vector2.zero);
            fill.anchorMax = Vector2.one; fill.offsetMin = fill.offsetMax = Vector2.zero;
            fill.gameObject.AddComponent<Image>().color = color; return fill;
        }
        internal static void Fill(RectTransform rect, double value) => rect.anchorMax = new Vector2(Mathf.Clamp01((float)value), 1);
    }
}
