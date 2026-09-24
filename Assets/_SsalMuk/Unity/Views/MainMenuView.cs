using System;
using System.Collections.Generic;
using SsalMuk.Core;
using SsalMuk.Presentation;
using UnityEngine;

namespace SsalMuk.Unity
{
    public sealed class MainMenuView : MonoBehaviour, IMainMenuView
    {
        private Canvas canvas;
        private CanvasGroup interaction;
        private UnityEngine.UI.Text status;
        private readonly List<UnityEngine.UI.Button> weaponButtons = new List<UnityEngine.UI.Button>();
        private readonly List<UnityEngine.UI.Text> selectionLabels = new List<UnityEngine.UI.Text>();
        private readonly List<WeaponKind> weaponKinds = new List<WeaponKind>();
        public UnityEngine.UI.Button StartButton { get; private set; }
        public UnityEngine.UI.Button SettingsButton { get; private set; }
        public UnityEngine.UI.Button QuitButton { get; private set; }
        public WeaponKind SelectedWeapon { get; private set; } = WeaponKind.Sword;
        public event Action StartRequested;
        public event Action SettingsRequested;
        public event Action QuitRequested;
        public void Initialize(Font font)
        {
            canvas = UiFactory.Canvas(transform, "MainMenuCanvas", 100);
            interaction = canvas.gameObject.AddComponent<CanvasGroup>();
            var background = UiFactory.Rect(canvas.transform, "Backdrop", Vector2.zero, Vector2.zero, Vector2.zero);
            background.anchorMax = Vector2.one; background.offsetMin = background.offsetMax = Vector2.zero;
            background.gameObject.AddComponent<UnityEngine.UI.Image>().color = UiFactory.Ink;
            UiFactory.Label(canvas.transform, "Eyebrow", font, "SSALMUK  /  SURVIVAL", 17, UiFactory.Mint, new Vector2(700, 40), new Vector2(0.5f, 0.5f), new Vector2(0, 268));
            UiFactory.Label(canvas.transform, "Title", font, "쌀먹", 88, Color.white, new Vector2(720, 135), new Vector2(0.5f, 0.5f), new Vector2(0, 183));
            UiFactory.Label(canvas.transform, "Subtitle", font, "끝없이 이어지는 생존", 23, UiFactory.Muted, new Vector2(720, 48), new Vector2(0.5f, 0.5f), new Vector2(0, 100));
            UiFactory.Label(canvas.transform, "WeaponHeading", font, "시작 무기 선택", 21, Color.white, new Vector2(720, 34), new Vector2(0.5f, 0.5f), new Vector2(0, 42));
            var choices = UiFactory.Rect(canvas.transform, "StartingWeapons", new Vector2(900, 132), new Vector2(.5f, .5f), new Vector2(0, -48));
            var row = choices.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            row.spacing = 12; row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = row.childForceExpandWidth = row.childForceExpandHeight = false;
            AddWeapon(WeaponKind.Sword, "철검", "전방 60도 베기");
            AddWeapon(WeaponKind.Spear, "철창", "전방으로 길게 찌르기");
            AddWeapon(WeaponKind.Axe, "철도끼", "주변을 한 바퀴 휘두르기");
            AddWeapon(WeaponKind.Fireball, "파이어볼", "멀리 날아가 폭발하는 마법");
            SelectWeapon(WeaponKind.Sword);
            var actions = UiFactory.Rect(canvas.transform, "MenuActions", new Vector2(900, 62), new Vector2(.5f, .5f), new Vector2(0, -178));
            var actionRow = actions.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            actionRow.spacing = 15; actionRow.childAlignment = TextAnchor.MiddleCenter;
            actionRow.childControlWidth = actionRow.childControlHeight = actionRow.childForceExpandWidth = actionRow.childForceExpandHeight = false;
            StartButton = UiFactory.Button(actions, "GameStart", font, "게임 시작", Vector2.zero);
            SettingsButton = UiFactory.Button(actions, "Settings", font, "설정", Vector2.zero);
            QuitButton = UiFactory.Button(actions, "Quit", font, "종료", Vector2.zero);
            UiFactory.Secondary(SettingsButton); UiFactory.Secondary(QuitButton);
            StartButton.onClick.AddListener(Submit);
            SettingsButton.onClick.AddListener(() => { if (CanInteract(SettingsButton)) SettingsRequested?.Invoke(); });
            QuitButton.onClick.AddListener(() => { if (CanInteract(QuitButton)) QuitRequested?.Invoke(); });
            status = UiFactory.Label(canvas.transform, "Status", font, "", 17, UiFactory.Muted, new Vector2(920, 40), new Vector2(0.5f, 0.5f), new Vector2(0, -241));
            UiFactory.Label(canvas.transform, "PlayGuide", font, "이동과 공격은 자동으로 진행됩니다.  ·  ESC 메뉴", 16, UiFactory.Muted, new Vector2(900, 40), new Vector2(0.5f, 0), new Vector2(0, 32));

            void AddWeapon(WeaponKind kind, string title, string description)
            {
                var card = UiFactory.Rect(choices, "StartWeapon_" + kind, new Vector2(216, 132), new Vector2(.5f, .5f), Vector2.zero);
                var surface = card.gameObject.AddComponent<UnityEngine.UI.Image>();
                var button = card.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = surface;
                button.onClick.AddListener(() => { if (CanInteract(button)) SelectWeapon(kind); });
                weaponKinds.Add(kind); weaponButtons.Add(button);
                UiFactory.Label(card, "Name", font, title, 24, Color.white, new Vector2(204, 40), new Vector2(.5f, .5f), new Vector2(0, 29));
                UiFactory.Label(card, "Description", font, description, 14, UiFactory.Muted, new Vector2(204, 35), new Vector2(.5f, .5f), new Vector2(0, -9));
                selectionLabels.Add(UiFactory.Label(card, "Selection", font, "", 15, UiFactory.Mint, new Vector2(200, 28), new Vector2(.5f, .5f), new Vector2(0, -45)));
            }
        }
        private void SelectWeapon(WeaponKind kind)
        {
            SelectedWeapon = kind;
            for (int i = 0; i < weaponButtons.Count; i++)
            {
                bool selected = weaponKinds[i] == kind;
                weaponButtons[i].targetGraphic.color = selected ? new Color32(32, 76, 66, 255) : new Color32(26, 42, 49, 255);
                selectionLabels[i].text = selected ? "선택됨" : "선택";
            }
        }
        private bool CanInteract(UnityEngine.UI.Button button) => canvas.gameObject.activeInHierarchy && interaction.interactable && button.interactable;
        private void Submit() { if (StartButton != null && CanInteract(StartButton)) StartRequested?.Invoke(); }
        public void SetBlocked(bool blocked) { interaction.interactable = interaction.blocksRaycasts = !blocked; }
        public void Show(bool visible, bool canStart, string message)
        {
            if (canvas == null) return;
            canvas.gameObject.SetActive(visible); StartButton.interactable = SettingsButton.interactable = QuitButton.interactable = canStart;
            foreach (var button in weaponButtons) button.interactable = canStart;
            status.text = message;
        }
        private void OnDestroy()
        {
            if (StartButton != null) StartButton.onClick.RemoveAllListeners();
            if (SettingsButton != null) SettingsButton.onClick.RemoveAllListeners();
            if (QuitButton != null) QuitButton.onClick.RemoveAllListeners();
            foreach (var button in weaponButtons) if (button != null) button.onClick.RemoveAllListeners();
            StartRequested = SettingsRequested = QuitRequested = null;
        }
    }
}
