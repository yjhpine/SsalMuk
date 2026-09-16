using System;
using SsalMuk.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace SsalMuk.Unity
{
    public sealed class ResultsView : MonoBehaviour, IResultsView
    {
        private Canvas canvas;
        private Text timeText;
        public event Action RestartRequested;
        public event Action MenuRequested;
        public Button RestartButton { get; private set; }
        public Button MenuButton { get; private set; }
        public Text Summary { get; private set; }
        public void Initialize(Font font)
        {
            canvas = UiFactory.Canvas(transform, "ResultsCanvas", 200);
            var background = UiFactory.Rect(canvas.transform, "Backdrop", Vector2.zero, Vector2.zero, Vector2.zero);
            background.anchorMax = Vector2.one; background.offsetMin = background.offsetMax = Vector2.zero;
            background.gameObject.AddComponent<Image>().color = UiFactory.Ink;
            UiFactory.Label(canvas.transform, "Eyebrow", font, "이번 생존의 기록", 25, UiFactory.Mint, new Vector2(800, 60), new Vector2(0.5f, 0.5f), new Vector2(0, 160));
            timeText = UiFactory.Label(canvas.transform, "SurvivalTime", font, "", 76, Color.white, new Vector2(1000, 120), new Vector2(0.5f, 0.5f), new Vector2(0, 62));
            Summary = UiFactory.Label(canvas.transform, "Summary", font, "", 24, UiFactory.Muted, new Vector2(900, 100), new Vector2(0.5f, 0.5f), new Vector2(0, -44));
            RestartButton = UiFactory.Button(canvas.transform, "Restart", font, "다시 시작", new Vector2(-160, -154));
            MenuButton = UiFactory.Button(canvas.transform, "MainMenu", font, "메인 메뉴", new Vector2(160, -154));
            RestartButton.onClick.AddListener(SubmitRestart); MenuButton.onClick.AddListener(SubmitMenu); canvas.gameObject.SetActive(false);
        }
        public void Show(bool visible, bool canSubmit, string survival, string kills, string level)
        {
            canvas.gameObject.SetActive(visible); RestartButton.interactable = MenuButton.interactable = visible && canSubmit;
            timeText.text = survival; Summary.text = "처치  " + kills + "\n최종 레벨  " + level;
        }
        private void SubmitRestart() { if (!RestartButton.interactable) return; Lock(); RestartRequested?.Invoke(); }
        private void SubmitMenu() { if (!MenuButton.interactable) return; Lock(); MenuRequested?.Invoke(); }
        private void Lock() { RestartButton.interactable = MenuButton.interactable = false; }
        private void OnDestroy()
        {
            if (RestartButton != null) RestartButton.onClick.RemoveListener(SubmitRestart);
            if (MenuButton != null) MenuButton.onClick.RemoveListener(SubmitMenu);
            RestartRequested = null; MenuRequested = null;
        }
    }
}
