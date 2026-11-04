using System;
using System.Collections.Generic;
using System.Linq;
using SsalMuk.Core;
using UnityEngine;
namespace SsalMuk.Unity
{
    public sealed class FusionView
    {
        private readonly Canvas canvas;
        private readonly UnityEngine.UI.Text randomText, reason;
        private readonly UnityEngine.UI.Button[] materials = new UnityEngine.UI.Button[3];
        private readonly ItemHover[] materialHovers = new ItemHover[3];
        private readonly ItemHover resultHover;
        private readonly ItemTooltip tooltip;
        private readonly List<long> selected = new List<long>(3);
        private readonly Action changed, clicked;
        private RunModel run;
        private FusionPreview preview;
        private long shownVersion = -1;
        private int shownSelection = -1;
        public int SelectionRevision { get; private set; }
        public bool IsOpen { get; private set; }
        public bool IsVisible => canvas.gameObject.activeSelf;
        public IReadOnlyList<long> SelectedIds { get; }
        public UnityEngine.UI.Button FixedButton { get; }
        public UnityEngine.UI.Button RandomButton { get; }
        public UnityEngine.UI.Text ResultText { get; }
        public FusionView(Transform parent, Font font, ItemTooltip tooltip, Action changed, Action clicked)
        {
            this.tooltip = tooltip; this.changed = changed; this.clicked = clicked; SelectedIds = selected.AsReadOnly();
            canvas = UiFactory.Canvas(parent, "FusionCanvas", 90);
            var panel = ItemUiStyle.Panel(canvas.transform, "FusionPanel", new Vector2(0,240), new Vector2(0,0), new Vector2(0,120)); panel.anchorMax = new Vector2(1,0);
            UiFactory.Label(panel, "Heading", font, "아이템 합성", 25, UnityEngine.Color.white, new Vector2(320,40), new Vector2(.5f,0), new Vector2(-400,210));
            for (int i = 0; i < 3; i++)
            {
                int slot = i;
                materials[i] = ItemUiStyle.Button(panel, font, "Material" + i, "+", new Vector2(74,64), new Vector2(.5f,0), new Vector2(-486 + i * 86,145));
                UiFactory.Secondary(materials[i]); materialHovers[i] = materials[i].gameObject.AddComponent<ItemHover>();
                materials[i].onClick.AddListener(() => { if (slot < selected.Count) Toggle(selected[slot]); });
            }
            reason = UiFactory.Label(panel, "Reason", font, "", 15, UiFactory.Muted, new Vector2(360,48), new Vector2(.5f,0), new Vector2(-400,84));
            UiFactory.Label(panel, "Consumption", font, "선택한 재료 3개와 효과가 사라집니다", 15, new Color32(255,190,160,255), new Vector2(390,42), new Vector2(.5f,0), new Vector2(-400,34));
            randomText = UiFactory.Label(panel, "RandomPool", font, "", 16, UnityEngine.Color.white, new Vector2(690,70), new Vector2(.5f,0), new Vector2(170,185), TextAnchor.MiddleLeft);
            ResultText = UiFactory.Label(panel, "FixedResult", font, "", 17, UiFactory.Muted, new Vector2(690,70), new Vector2(.5f,0), new Vector2(170,111), TextAnchor.MiddleLeft);
            ResultText.raycastTarget = true; resultHover = ResultText.gameObject.AddComponent<ItemHover>();
            RandomButton = ItemUiStyle.Button(panel, font, "RandomFusion", "랜덤 합성", new Vector2(205,42), new Vector2(.5f,0), new Vector2(65,35));
            FixedButton = ItemUiStyle.Button(panel, font, "FixedFusion", "확정 합성", new Vector2(205,42), new Vector2(.5f,0), new Vector2(295,35));
            RandomButton.onClick.AddListener(() => Submit(FusionMode.Random)); FixedButton.onClick.AddListener(() => Submit(FusionMode.Fixed));
            var close = ItemUiStyle.Button(panel, font, "CloseFusion", "×", new Vector2(38,32), new Vector2(1,1), new Vector2(-28,-25));
            close.onClick.AddListener(() => { clicked(); IsOpen = false; canvas.gameObject.SetActive(false); changed(); }); canvas.gameObject.SetActive(false);
        }
        public void ToggleOpen()
        { if (run == null || run.IsPaused || run.CurrentOffer != null || run.HasItemChoice) return; clicked(); IsOpen = !IsOpen; changed(); }
        public void Toggle(long id)
        {
            if (!IsVisible || run == null || run.IsPaused || run.Fusion.HasQueuedCommand || !run.Inventory.TryGet(id, out _)) return;
            clicked(); if (!selected.Remove(id)) { if (selected.Count == 3) return; selected.Add(id); }
            SelectionRevision++; changed();
        }
        public void Refresh(RunModel model, bool visible)
        {
            if (run != model) { run = model; selected.Clear(); IsOpen = false; shownVersion = -1; SelectionRevision++; }
            if (run == null) { canvas.gameObject.SetActive(false); return; }
            if (shownVersion != run.Inventory.Version)
            { if (selected.RemoveAll(id => !run.Inventory.TryGet(id, out _)) > 0) SelectionRevision++; }
            canvas.gameObject.SetActive(visible && IsOpen);
            if (!IsVisible) return;
            if (shownVersion != run.Inventory.Version || shownSelection != SelectionRevision)
            {
                shownVersion = run.Inventory.Version; shownSelection = SelectionRevision; preview = run.Fusion.Preview(selected);
                for (int i = 0; i < 3; i++)
                {
                    ItemDefinition def = i < selected.Count && run.Inventory.TryGet(selected[i], out var item) ? item.Definition : null;
                    var label = materials[i].GetComponentInChildren<UnityEngine.UI.Text>(); label.text = def?.Symbol ?? "+";
                    label.color = def == null ? UiFactory.Muted : ItemUiStyle.Color(def.Rarity);
                    materialHovers[i].Bind(tooltip, def, run.Inventory.Catalog);
                }
                reason.text = preview.IsValid ? "재료를 다시 누르면 선택 해제" : preview.Reason;
                randomText.text = preview.IsValid ? "랜덤 결과 · " + ItemUiStyle.RarityName(preview.RandomResults[0].Rarity) + "\n" + string.Join(" · ", preview.RandomResults.Select(d => d.Name)) : "랜덤 결과 · 같은 등급 재료 3개 필요";
                ResultText.text = preview.FixedResult == null ? "일치하는 확정 레시피가 없습니다" : "확정 결과 · " + preview.FixedResult.Name + "\n마우스를 올려 효과와 조합 확인";
                ResultText.color = preview.FixedResult == null ? UiFactory.Muted : ItemUiStyle.Color(preview.FixedResult.Rarity);
                resultHover.Bind(tooltip, preview.FixedResult, run.Inventory.Catalog);
            }
            bool ready = !run.Fusion.HasQueuedCommand && !run.IsPaused;
            RandomButton.interactable = ready && preview.IsValid; FixedButton.interactable = ready && preview.IsValid && preview.FixedResult != null;
            foreach (var material in materials) material.interactable = ready;
        }
        private void Submit(FusionMode mode)
        {
            if (!IsVisible || !(mode == FusionMode.Random ? RandomButton : FixedButton).interactable) return;
            clicked(); run.Fusion.TryQueue(new FusionCommand(run.Id, run.Inventory.Version, selected, mode, preview.RecipeId)); changed();
        }
    }
}
