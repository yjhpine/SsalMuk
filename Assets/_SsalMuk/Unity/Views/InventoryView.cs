using System;
using System.Collections.Generic;
using SsalMuk.Core;
using UnityEngine;
namespace SsalMuk.Unity
{
    public sealed class InventorySlot
    {
        private readonly UnityEngine.UI.Text symbol;
        private readonly UnityEngine.UI.Outline outline;
        private readonly ItemHover hover;
        public long ItemId { get; private set; }
        public UnityEngine.UI.Button Button { get; }
        internal InventorySlot(Transform parent, Font font, Action<long> clicked)
        {
            Button = ItemUiStyle.Button(parent, font, "ItemSlot", "", new Vector2(46,46), new Vector2(0,1), Vector2.zero);
            UiFactory.Secondary(Button); symbol = Button.GetComponentInChildren<UnityEngine.UI.Text>(); symbol.fontSize = 24;
            outline = Button.gameObject.AddComponent<UnityEngine.UI.Outline>(); outline.effectDistance = new Vector2(1,-1);
            hover = Button.gameObject.AddComponent<ItemHover>(); Button.onClick.AddListener(() => { if (Button.interactable && ItemId != 0) clicked(ItemId); });
        }
        internal void Bind(OwnedItem item, ItemCatalog catalog, ItemTooltip tooltip, bool selected, bool canSelect, int row, int column)
        {
            ItemId = item?.Id ?? 0; Button.gameObject.SetActive(item != null); if (item == null) return;
            ((RectTransform)Button.transform).anchoredPosition = new Vector2(25 + column * 52, -25 - row * 52);
            symbol.text = item.Definition.Symbol; symbol.color = ItemUiStyle.Color(item.Definition.Rarity);
            outline.effectColor = selected ? UnityEngine.Color.white : symbol.color; outline.effectDistance = selected ? new Vector2(3,-3) : new Vector2(1,-1);
            Button.interactable = canSelect; hover.Bind(tooltip, item.Definition, catalog);
        }
    }
    public sealed class InventoryView
    {
        private readonly RectTransform panel, content;
        private readonly UnityEngine.UI.Text heading;
        private readonly ItemTooltip tooltip;
        private readonly List<InventorySlot> slots = new List<InventorySlot>();
        private RunModel run;
        private IReadOnlyList<long> selected;
        private long shownVersion = -1;
        private int selectionVersion = -1;
        private bool canSelect;
        public IReadOnlyList<InventorySlot> Slots { get; }
        public UnityEngine.UI.ScrollRect Scroll { get; }
        public UnityEngine.UI.Button FusionButton { get; }
        public InventoryView(Transform parent, Font font, ItemTooltip tooltip, Action<long> clicked, Action toggleFusion)
        {
            this.tooltip = tooltip;
            panel = ItemUiStyle.Panel(parent, "Inventory", new Vector2(228,360), new Vector2(1,1), new Vector2(-126,-330));
            heading = UiFactory.Label(panel, "Heading", font, "아이템", 20, UnityEngine.Color.white, new Vector2(208,34), new Vector2(.5f,1), new Vector2(0,-23));
            var scrollRoot = UiFactory.Rect(panel, "Scroll", new Vector2(208,252), new Vector2(.5f,1), new Vector2(0,-171));
            Scroll = scrollRoot.gameObject.AddComponent<UnityEngine.UI.ScrollRect>(); Scroll.horizontal = false;
            Scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped; Scroll.scrollSensitivity = 30;
            var viewport = UiFactory.Rect(scrollRoot, "Viewport", Vector2.zero, Vector2.zero, Vector2.zero);
            viewport.anchorMax = Vector2.one; viewport.offsetMin = viewport.offsetMax = Vector2.zero;
            viewport.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color32(15,30,39,255);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            content = UiFactory.Rect(viewport, "Content", new Vector2(0,252), new Vector2(.5f,1), Vector2.zero);
            content.anchorMin = new Vector2(0,1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f,1);
            Scroll.viewport = viewport; Scroll.content = content;
            // Virtual rows own the content height; a layout group would size it from pooled slots instead of the inventory.
            for (int i = 0; i < 28; i++) slots.Add(new InventorySlot(content, font, clicked));
            Slots = slots.AsReadOnly(); Scroll.onValueChanged.AddListener(_ => Rebind());
            FusionButton = ItemUiStyle.Button(panel, font, "OpenFusion", "합성", new Vector2(204,38), new Vector2(.5f,0), new Vector2(0,29));
            FusionButton.onClick.AddListener(() => { if (FusionButton.interactable) toggleFusion(); });
        }
        public void Refresh(RunModel model, IReadOnlyList<long> selection, int revision, bool inputEnabled, bool fusionOpen)
        {
            bool changed = run != model || shownVersion != model.Inventory.Version || selectionVersion != revision || canSelect != (inputEnabled && fusionOpen);
            if (run != model) { content.anchoredPosition = Vector2.zero; Scroll.StopMovement(); }
            run = model; selected = selection; canSelect = inputEnabled && fusionOpen; selectionVersion = revision;
            FusionButton.interactable = inputEnabled;
            if (!changed) return;
            shownVersion = run.Inventory.Version; heading.text = "아이템  " + run.Inventory.Items.Count;
            float height = Mathf.Max(252, ((run.Inventory.Items.Count + 3) / 4) * 52);
            content.sizeDelta = new Vector2(0, height);
            content.anchoredPosition = new Vector2(0, Mathf.Clamp(content.anchoredPosition.y, 0, height - 252)); Rebind();
        }
        private void Rebind()
        {
            if (run == null) return;
            int first = Mathf.Max(0, Mathf.FloorToInt(content.anchoredPosition.y / 52) - 1);
            for (int i = 0; i < slots.Count; i++)
            {
                int row = first + i / 4, index = row * 4 + i % 4;
                var item = index < run.Inventory.Items.Count ? run.Inventory.Items[index] : null;
                bool chosen = false; if (item != null && selected != null) for (int j = 0; j < selected.Count; j++) if (selected[j] == item.Id) chosen = true;
                slots[i].Bind(item, run.Inventory.Catalog, tooltip, chosen, canSelect, row, i % 4);
            }
        }
    }
}
