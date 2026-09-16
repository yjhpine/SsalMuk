using System;
using SsalMuk.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace SsalMuk.Unity
{
    public sealed class MainMenuView : MonoBehaviour, IMainMenuView
    {
        private Canvas canvas;
        private Text status;
        public Button StartButton { get; private set; }
        public event Action StartRequested;
        public void Initialize(Font font)
        {
            canvas = UiFactory.Canvas(transform, "MainMenuCanvas", 100);
            var background = UiFactory.Rect(canvas.transform, "Backdrop", Vector2.zero, Vector2.zero, Vector2.zero);
            background.anchorMax = Vector2.one; background.offsetMin = background.offsetMax = Vector2.zero;
            background.gameObject.AddComponent<Image>().color = UiFactory.Ink;
            UiFactory.Label(canvas.transform, "Eyebrow", font, "SSALMUK  /  SURVIVAL", 17, UiFactory.Mint, new Vector2(700, 40), new Vector2(0.5f, 0.5f), new Vector2(0, 162));
            UiFactory.Label(canvas.transform, "Title", font, "쌀먹", 88, Color.white, new Vector2(720, 150), new Vector2(0.5f, 0.5f), new Vector2(0, 65));
            UiFactory.Label(canvas.transform, "Subtitle", font, "끝없이 이어지는 생존", 25, UiFactory.Muted, new Vector2(720, 60), new Vector2(0.5f, 0.5f), new Vector2(0, -30));
            var buttonRect = UiFactory.Rect(canvas.transform, "GameStart", new Vector2(320, 64), new Vector2(0.5f, 0.5f), new Vector2(0, -128));
            buttonRect.gameObject.AddComponent<Image>().color = UiFactory.Mint;
            StartButton = buttonRect.gameObject.AddComponent<Button>(); StartButton.targetGraphic = buttonRect.GetComponent<Image>();
            var colors = StartButton.colors; colors.highlightedColor = new Color(0.9f, 1, 0.94f); colors.pressedColor = new Color(0.7f, 0.88f, 0.8f); colors.disabledColor = new Color(0.35f, 0.5f, 0.46f); StartButton.colors = colors;
            UiFactory.Label(buttonRect, "Caption", font, "GameStart", 27, UiFactory.Ink, new Vector2(300, 60), new Vector2(0.5f, 0.5f), Vector2.zero);
            StartButton.onClick.AddListener(Submit);
            status = UiFactory.Label(canvas.transform, "Status", font, "", 17, UiFactory.Muted, new Vector2(920, 60), new Vector2(0.5f, 0.5f), new Vector2(0, -198));
            UiFactory.Label(canvas.transform, "PlayGuide", font, "이동과 공격은 자동으로 진행됩니다.", 16, UiFactory.Muted, new Vector2(900, 40), new Vector2(0.5f, 0), new Vector2(0, 32));
        }
        private void Submit() { if (StartButton != null && StartButton.interactable) StartRequested?.Invoke(); }
        public void Show(bool visible, bool canStart, string message)
        {
            if (canvas == null) return;
            canvas.gameObject.SetActive(visible); StartButton.interactable = canStart; status.text = message;
        }
        private void OnDestroy() { if (StartButton != null) StartButton.onClick.RemoveListener(Submit); StartRequested = null; }
    }
}
