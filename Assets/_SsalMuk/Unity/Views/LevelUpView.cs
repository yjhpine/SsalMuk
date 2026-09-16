using System;
using System.Collections.Generic;
using SsalMuk.Presentation;
using UnityEngine;
using UnityEngine.UI;
namespace SsalMuk.Unity
{
    public sealed class LevelUpView : MonoBehaviour, ILevelUpView
    {
        private Canvas canvas;
        private readonly List<Button> buttons = new List<Button>();
        private readonly List<Text> titles = new List<Text>(), descriptions = new List<Text>();
        public event Action<int> ChoiceRequested;
        public IReadOnlyList<Button> Buttons { get; private set; } = Array.Empty<Button>();
        public bool IsVisible { get; private set; }
        public Text RemainingText { get; private set; }
        public IReadOnlyList<Text> Titles { get; private set; } = Array.Empty<Text>();
        public void Initialize(Font font)
        {
            canvas = UiFactory.Canvas(transform, "LevelUpCanvas", 100);
            var panel = UiFactory.Rect(canvas.transform, "Backdrop", new Vector2(0, 220), new Vector2(0, 0), new Vector2(0, 110));
            panel.anchorMax = new Vector2(1, 0); var background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.03f, 0.07f, 0.08f, 0.96f); background.raycastTarget = false;
            UiFactory.Label(canvas.transform, "Heading", font, "레벨업", 27, UiFactory.Mint, new Vector2(240, 60),
                new Vector2(0, 0), new Vector2(195, 177), TextAnchor.MiddleLeft);
            RemainingText = UiFactory.Label(canvas.transform, "Remaining", font, "", 19, UiFactory.Muted, new Vector2(690, 48),
                new Vector2(1, 0), new Vector2(-420, 176), TextAnchor.MiddleRight);
            for (int i = 0; i < 3; i++)
            {
                int slot = i;
                var card = UiFactory.Rect(canvas.transform, "Choice" + i, new Vector2(350, 112), new Vector2(0.5f, 0), new Vector2((i - 1) * 370, 91));
                var image = card.gameObject.AddComponent<Image>(); image.color = new Color32(42, 68, 70, 255);
                var button = card.gameObject.AddComponent<Button>(); button.targetGraphic = image;
                var colors = button.colors; colors.highlightedColor = new Color(1.3f, 1.4f, 1.3f); colors.pressedColor = UiFactory.Mint; button.colors = colors;
                button.onClick.AddListener(() => Submit(slot)); buttons.Add(button);
                var mark = UiFactory.Rect(card, "Accent", new Vector2(4, 112), new Vector2(0, 0.5f), new Vector2(2, 0));
                mark.gameObject.AddComponent<Image>().color = UiFactory.Mint;
                titles.Add(UiFactory.Label(card, "Title", font, "", 23, Color.white, new Vector2(318, 52), new Vector2(0.5f, 0.5f), new Vector2(0, 27)));
                descriptions.Add(UiFactory.Label(card, "Description", font, "", 15, UiFactory.Muted, new Vector2(318, 50), new Vector2(0.5f, 0.5f), new Vector2(0, -26)));
            }
            Buttons = buttons.AsReadOnly(); Titles = titles.AsReadOnly(); canvas.gameObject.SetActive(false);
        }
        public void Show(bool visible, bool canSubmit, string remaining, IReadOnlyList<string> titleValues, IReadOnlyList<string> descriptionValues)
        {
            IsVisible = visible; canvas.gameObject.SetActive(visible);
            if (!visible) return;
            RemainingText.text = remaining;
            for (int i = 0; i < buttons.Count; i++)
            { buttons[i].interactable = canSubmit; titles[i].text = titleValues[i]; descriptions[i].text = descriptionValues[i]; }
        }
        private void Submit(int slot)
        {
            if (!IsVisible || !buttons[slot].interactable) return;
            foreach (var button in buttons) button.interactable = false;
            ChoiceRequested?.Invoke(slot);
        }
        private void OnDestroy() { foreach (var button in buttons) if (button != null) button.onClick.RemoveAllListeners(); ChoiceRequested = null; }
    }
}
