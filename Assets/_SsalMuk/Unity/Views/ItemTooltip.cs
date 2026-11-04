using SsalMuk.Core;
using UnityEngine;
using UnityEngine.EventSystems;
namespace SsalMuk.Unity
{
    public sealed class ItemTooltip
    {
        private readonly UnityEngine.UI.Text description;
        private object owner;
        public bool IsVisible => Rect.gameObject.activeSelf;
        public UnityEngine.UI.Text NameText { get; }
        public RectTransform Rect { get; }
        public RectTransform CanvasRect { get; }
        public ItemTooltip(Transform parent, Font font)
        {
            var canvas = UiFactory.Canvas(parent, "ItemTooltipCanvas", 150); CanvasRect = (RectTransform)canvas.transform;
            Rect = ItemUiStyle.Panel(canvas.transform, "Tooltip", new Vector2(370, 260), new Vector2(.5f,.5f), Vector2.zero);
            Rect.pivot = new Vector2(0,1); Rect.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            NameText = UiFactory.Label(Rect, "Name", font, "", 23, UnityEngine.Color.white, new Vector2(346,36), new Vector2(0,1), new Vector2(12,-10), TextAnchor.UpperLeft);
            NameText.rectTransform.pivot = new Vector2(0,1);
            description = UiFactory.Label(Rect, "Details", font, "", 14, new Color32(226,234,239,255), new Vector2(346,200), new Vector2(0,1), new Vector2(12,-50), TextAnchor.UpperLeft);
            description.rectTransform.pivot = new Vector2(0,1); Rect.gameObject.SetActive(false);
        }
        public void Show(object source, ItemDefinition item, ItemCatalog catalog, Vector2 screenPoint)
        {
            owner = source; NameText.text = item.Name; NameText.color = ItemUiStyle.Color(item.Rarity);
            string recipes = ItemUiStyle.Recipes(item, catalog);
            description.text = ItemUiStyle.RarityName(item.Rarity) + "\n" + ItemUiStyle.Description(item) + (recipes.Length > 0 ? "\n\n확정 합성\n" + recipes : "");
            float height = Mathf.Min(Mathf.Max(160, description.preferredHeight + 66), CanvasRect.rect.height - 16);
            Rect.sizeDelta = new Vector2(370, height); description.rectTransform.sizeDelta = new Vector2(346, height - 58);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(CanvasRect, screenPoint, null, out var point);
            var bounds = CanvasRect.rect;
            Rect.anchoredPosition = new Vector2(Mathf.Clamp(point.x + 16, bounds.xMin + 8, bounds.xMax - 378), Mathf.Clamp(point.y - 16, bounds.yMin + height + 8, bounds.yMax - 8));
            Rect.gameObject.SetActive(true);
        }
        public void Hide(object source = null) { if (source != null && source != owner) return; owner = null; Rect.gameObject.SetActive(false); }
    }
    public sealed class ItemHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private ItemTooltip tooltip;
        private ItemDefinition item;
        private ItemCatalog catalog;
        private bool hovered;
        private Vector2 pointer;
        public void Bind(ItemTooltip target, ItemDefinition definition, ItemCatalog definitions)
        {
            if (item == definition && tooltip == target && catalog == definitions) return;
            tooltip?.Hide(this); tooltip = target; item = definition; catalog = definitions;
            if (hovered && item != null) tooltip.Show(this, item, catalog, pointer);
        }
        public void OnPointerEnter(PointerEventData data)
        { hovered = true; pointer = data.position; if (item != null) tooltip?.Show(this, item, catalog, pointer); }
        public void OnPointerExit(PointerEventData data) { hovered = false; tooltip?.Hide(this); }
        private void OnDisable() { hovered = false; tooltip?.Hide(this); }
    }
}
