using SsalMuk.Core;
using SsalMuk.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SsalMuk.Unity
{
    public sealed class MenuController : MonoBehaviour, IMenuNavigationView
    {
        private AppRoot app;
        private DisplaySettings display;
        private MenuNavigationPresenter navigation;
        public PauseMenuView PauseMenu { get; private set; }
        public SettingsView Settings { get; private set; }
        public void Initialize(AppRoot root, Font font, DisplaySettings displaySettings)
        {
            app = root; display = displaySettings;
            var pause = new GameObject("PauseMenu"); pause.transform.SetParent(transform, false);
            PauseMenu = pause.AddComponent<PauseMenuView>(); PauseMenu.Initialize(font);
            var settings = new GameObject("SettingsMenu"); settings.transform.SetParent(transform, false);
            Settings = settings.AddComponent<SettingsView>(); Settings.Initialize(font);
            navigation = new MenuNavigationPresenter(app.Coordinator, this);
            app.Menu.SettingsRequested += OpenSettings; app.Menu.QuitRequested += Quit;
            PauseMenu.ResumeRequested += Resume; PauseMenu.SettingsRequested += OpenSettings;
            Settings.BackRequested += CloseSettings; Settings.DarkModeChanged += display.SetDarkMode;
        }
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) HandleEscape();
        }
        public void HandleEscape()
        {
            if (app == null) return;
            navigation.HandleEscape();
        }
        private void OpenSettings() => navigation.OpenSettings();
        private void CloseSettings() => navigation.CloseSettings();
        private void Resume() => navigation.Resume();
        bool IMenuNavigationView.IsSettingsVisible => Settings.IsVisible;
        void IMenuNavigationView.SetMenuBlocked(bool blocked) => app.Menu.SetBlocked(blocked);
        void IMenuNavigationView.ShowPause(bool visible) => PauseMenu.Show(visible);
        void IMenuNavigationView.ShowSettings(bool visible) => Settings.Show(visible, display.DarkMode);
        void IMenuNavigationView.Focus(MenuFocus target)
        {
            GameObject selected = target == MenuFocus.PauseResume ? PauseMenu.ResumeButton.gameObject :
                target == MenuFocus.PauseSettings ? PauseMenu.SettingsButton.gameObject :
                target == MenuFocus.MainMenuSettings ? app.Menu.SettingsButton.gameObject :
                target == MenuFocus.DarkMode ? Settings.DarkModeToggle.gameObject : null;
            Focus(selected);
        }
        private void Quit()
        {
            if (app.Coordinator.Phase != RunPhase.MainMenu || Settings.IsVisible) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        private static void Focus(GameObject selected)
        { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(selected); }
        private void OnDestroy()
        {
            navigation?.Dispose();
            if (app != null)
            {
                if (app.Menu != null) { app.Menu.SettingsRequested -= OpenSettings; app.Menu.QuitRequested -= Quit; }
            }
            if (PauseMenu != null) { PauseMenu.ResumeRequested -= Resume; PauseMenu.SettingsRequested -= OpenSettings; }
            if (Settings != null) { Settings.BackRequested -= CloseSettings; Settings.DarkModeChanged -= display.SetDarkMode; }
        }
    }
}
