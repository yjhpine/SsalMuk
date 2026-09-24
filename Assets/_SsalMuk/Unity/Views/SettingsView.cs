using System;
using UnityEngine;

namespace SsalMuk.Unity
{
    public sealed class SettingsView : MonoBehaviour
    {
        private Canvas canvas;
        private UnityEngine.UI.Text state;
        public bool IsVisible => canvas != null && canvas.gameObject.activeSelf;
        public UnityEngine.UI.Toggle DarkModeToggle { get; private set; }
        public UnityEngine.UI.Button BackButton { get; private set; }
        public event Action<bool> DarkModeChanged;
        public event Action BackRequested;
        public void Initialize(Font font)
        {
            canvas = UiFactory.Modal(transform, "SettingsCanvas", font, "설정", 210);
            var row = UiFactory.Rect(canvas.transform, "DarkMode", new Vector2(440, 66), new Vector2(.5f, .5f), new Vector2(0, 40));
            var surface = row.gameObject.AddComponent<UnityEngine.UI.Image>(); surface.color = new Color32(30, 48, 55, 255);
            DarkModeToggle = row.gameObject.AddComponent<UnityEngine.UI.Toggle>(); DarkModeToggle.targetGraphic = surface;
            DarkModeToggle.toggleTransition = UnityEngine.UI.Toggle.ToggleTransition.None;
            UiFactory.Label(row, "Label", font, "다크모드", 23, Color.white, new Vector2(220, 60), new Vector2(0, .5f), new Vector2(135, 0), TextAnchor.MiddleLeft);
            state = UiFactory.Label(row, "State", font, "", 18, UiFactory.Mint, new Vector2(65, 60), new Vector2(1, .5f), new Vector2(-110, 0));
            var box = UiFactory.Rect(row, "Box", new Vector2(32, 32), new Vector2(1, .5f), new Vector2(-43, 0));
            var boxImage = box.gameObject.AddComponent<UnityEngine.UI.Image>(); boxImage.color = UiFactory.Ink; boxImage.raycastTarget = false;
            var check = UiFactory.Rect(box, "Checkmark", new Vector2(20, 20), new Vector2(.5f, .5f), Vector2.zero);
            var checkImage = check.gameObject.AddComponent<UnityEngine.UI.Image>(); checkImage.color = UiFactory.Mint; checkImage.raycastTarget = false;
            DarkModeToggle.graphic = checkImage;
            DarkModeToggle.onValueChanged.AddListener(ChangeDarkMode);
            UiFactory.Label(canvas.transform, "Description", font, "전투 배경을 어둡게 표시합니다.\n변경한 설정은 자동으로 저장됩니다.", 17, UiFactory.Muted,
                new Vector2(460, 65), new Vector2(.5f, .5f), new Vector2(0, -35));
            BackButton = UiFactory.Button(canvas.transform, "Back", font, "돌아가기", new Vector2(0, -122));
            UiFactory.Secondary(BackButton);
            BackButton.onClick.AddListener(() => { if (IsVisible) BackRequested?.Invoke(); });
            Show(false, true);
        }
        private void ChangeDarkMode(bool enabled)
        {
            if (!IsVisible) return;
            state.text = enabled ? "켜짐" : "꺼짐"; DarkModeChanged?.Invoke(enabled);
        }
        public void Show(bool visible, bool darkMode)
        {
            DarkModeToggle.SetIsOnWithoutNotify(darkMode); state.text = darkMode ? "켜짐" : "꺼짐";
            canvas.gameObject.SetActive(visible);
        }
        private void OnDestroy()
        {
            if (DarkModeToggle != null) DarkModeToggle.onValueChanged.RemoveAllListeners();
            if (BackButton != null) BackButton.onClick.RemoveAllListeners();
            BackRequested = null; DarkModeChanged = null;
        }
    }
}
