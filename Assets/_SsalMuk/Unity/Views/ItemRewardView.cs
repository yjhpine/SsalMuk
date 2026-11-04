using System;
using System.Collections.Generic;
using SsalMuk.Core;
using UnityEngine;
namespace SsalMuk.Unity
{
    public sealed class ItemRewardView
    {
        private readonly Canvas canvas;
        private readonly List<UnityEngine.UI.Button> buttons = new List<UnityEngine.UI.Button>();
        private readonly UnityEngine.UI.Text[] titles = new UnityEngine.UI.Text[3], details = new UnityEngine.UI.Text[3];
        private readonly UnityEngine.UI.Image[] accents = new UnityEngine.UI.Image[3];
        private readonly ItemHover[] hovers = new ItemHover[3];
        private readonly UnityEngine.UI.Text remaining;
        private readonly ItemTooltip tooltip;
        private readonly Action clicked;
        private RunModel run;
        private ItemOffer shown;
        public bool IsVisible => canvas.gameObject.activeSelf;
        public IReadOnlyList<UnityEngine.UI.Button> Buttons { get; }
        public IReadOnlyList<UnityEngine.UI.Text> Titles => titles;
        public UnityEngine.UI.Text Heading { get; }
        public ItemRewardView(Transform parent, Font font, ItemTooltip tooltip, Action clicked)
        {
            this.tooltip = tooltip; this.clicked = clicked; canvas = UiFactory.Canvas(parent, "ItemRewardCanvas", 100);
            var panel = ItemUiStyle.Panel(canvas.transform, "ItemRewards", new Vector2(0,230), new Vector2(0,0), new Vector2(0,115)); panel.anchorMax = new Vector2(1,0);
            Heading = UiFactory.Label(panel, "Heading", font, "상자 보상", 27, UiFactory.Mint, new Vector2(300,45), new Vector2(0,1), new Vector2(220,-34), TextAnchor.MiddleLeft);
            remaining = UiFactory.Label(panel, "Remaining", font, "", 17, UiFactory.Muted, new Vector2(650,45), new Vector2(1,1), new Vector2(-410,-34), TextAnchor.MiddleRight);
            for (int i = 0; i < 3; i++)
            {
                int slot = i; var card = ItemUiStyle.Panel(panel, "ItemChoice" + i, new Vector2(350,136), new Vector2(.5f,0), new Vector2((i-1)*370,91));
                var button = card.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = card.GetComponent<UnityEngine.UI.Image>();
                button.onClick.AddListener(() => Choose(slot)); buttons.Add(button);
                accents[i] = UiFactory.Rect(card, "Accent", new Vector2(4,136), new Vector2(0,.5f), new Vector2(2,0)).gameObject.AddComponent<UnityEngine.UI.Image>();
                titles[i] = UiFactory.Label(card, "Name", font, "", 23, UnityEngine.Color.white, new Vector2(320,43), new Vector2(.5f,1), new Vector2(0,-26));
                details[i] = UiFactory.Label(card, "Effect", font, "", 14, new Color32(220,231,236,255), new Vector2(320,70), new Vector2(.5f,0), new Vector2(0,48));
                hovers[i] = card.gameObject.AddComponent<ItemHover>();
            }
            Buttons = buttons.AsReadOnly(); canvas.gameObject.SetActive(false);
        }
        public void Refresh(RunModel model)
        {
            run = model; var offer = model?.ItemRewards.CurrentOffer;
            bool visible = run != null && run.Phase == RunPhase.Running && !run.IsPaused && offer != null;
            canvas.gameObject.SetActive(visible); if (!visible) { shown = null; return; }
            remaining.text = "남은 상자 " + run.ItemRewards.PendingCount + "개 · 3개 중 1개 직접 선택";
            if (shown != offer)
            {
                shown = offer;
                for (int i = 0; i < 3; i++)
                {
                    var item = offer.Choices[i]; titles[i].text = item.Symbol + "  " + item.Name; titles[i].color = accents[i].color = ItemUiStyle.Color(item.Rarity);
                    string detail = ItemUiStyle.Description(item); int newline = detail.IndexOf('\n');
                    details[i].text = ItemUiStyle.RarityName(item.Rarity) + "\n" + (newline < 0 ? detail : detail.Substring(0,newline)) + "\n마우스를 올려 상세 효과 확인";
                    hovers[i].Bind(tooltip, item, run.Inventory.Catalog);
                }
            }
            foreach (var button in buttons) button.interactable = !run.ItemRewards.HasQueuedChoice;
        }
        private void Choose(int slot)
        {
            if (!IsVisible || !buttons[slot].interactable || shown == null) return;
            clicked(); if (run.ItemRewards.TryQueueChoice(run.Id, shown.Id, slot)) foreach (var button in buttons) button.interactable = false;
        }
    }
}
