using System;
using UnityEngine;

namespace SsalMuk.Unity
{
    public sealed class PauseMenuView : MonoBehaviour
    {
        private Canvas canvas;
        public bool IsVisible => canvas != null && canvas.gameObject.activeSelf;
        public UnityEngine.UI.Button ResumeButton { get; private set; }
        public UnityEngine.UI.Button SettingsButton { get; private set; }
        public event Action ResumeRequested;
        public event Action SettingsRequested;
        public void Initialize(Font font)
        {
            canvas = UiFactory.Modal(transform, "PauseCanvas", font, "일시 정지", 200);
            var actions = UiFactory.Rect(canvas.transform, "PauseActions", new Vector2(290, 144), new Vector2(.5f, .5f), new Vector2(0, -5));
            var column = actions.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            column.spacing = 20; column.childAlignment = TextAnchor.MiddleCenter;
            column.childControlWidth = column.childControlHeight = column.childForceExpandWidth = column.childForceExpandHeight = false;
            ResumeButton = UiFactory.Button(actions, "Resume", font, "게임 재개", Vector2.zero);
            SettingsButton = UiFactory.Button(actions, "Settings", font, "설정", Vector2.zero);
            UiFactory.Secondary(SettingsButton);
            ResumeButton.onClick.AddListener(() => { if (IsVisible) ResumeRequested?.Invoke(); });
            SettingsButton.onClick.AddListener(() => { if (IsVisible) SettingsRequested?.Invoke(); });
            UiFactory.Label(canvas.transform, "Guide", font, "ESC를 누르면 게임으로 돌아갑니다.", 16, UiFactory.Muted,
                new Vector2(480, 40), new Vector2(.5f, .5f), new Vector2(0, -140));
            Show(false);
        }
        public void Show(bool visible) => canvas.gameObject.SetActive(visible);
        private void OnDestroy()
        {
            if (ResumeButton != null) ResumeButton.onClick.RemoveAllListeners();
            if (SettingsButton != null) SettingsButton.onClick.RemoveAllListeners();
            ResumeRequested = SettingsRequested = null;
        }
    }
}
