using SsalMuk.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SsalMuk.Unity
{
    public sealed class MenuController : MonoBehaviour
    {
        private AppRoot app;
        private DisplaySettings display;
        public PauseMenuView PauseMenu { get; private set; }
        public SettingsView Settings { get; private set; }
        public void Initialize(AppRoot root, Font font, DisplaySettings displaySettings)
        {
            app = root; display = displaySettings;
            var pause = new GameObject("PauseMenu"); pause.transform.SetParent(transform, false);
            PauseMenu = pause.AddComponent<PauseMenuView>(); PauseMenu.Initialize(font);
            var settings = new GameObject("SettingsMenu"); settings.transform.SetParent(transform, false);
            Settings = settings.AddComponent<SettingsView>(); Settings.Initialize(font);
            app.Menu.SettingsRequested += OpenSettings; app.Menu.QuitRequested += Quit;
            PauseMenu.ResumeRequested += Resume; PauseMenu.SettingsRequested += OpenSettings;
            Settings.BackRequested += CloseSettings; Settings.DarkModeChanged += display.SetDarkMode;
            app.Coordinator.PhaseChanged += PhaseChanged;
        }
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) HandleEscape();
        }
        public void HandleEscape()
        {
            if (app == null) return;
            if (Settings.IsVisible) { CloseSettings(); return; }
            if (app.Coordinator.Phase != RunPhase.Running) return;
            if (app.Coordinator.Run.IsPaused) { Resume(); return; }
            if (app.Coordinator.Pause()) { PauseMenu.Show(true); Focus(PauseMenu.ResumeButton.gameObject); }
        }
        private void OpenSettings()
        {
            bool inMenu = app.Coordinator.Phase == RunPhase.MainMenu;
            if (!inMenu && (app.Coordinator.Phase != RunPhase.Running || !app.Coordinator.Run.IsPaused)) return;
            app.Menu.SetBlocked(true); PauseMenu.Show(false); Settings.Show(true, display.DarkMode);
            Focus(Settings.DarkModeToggle.gameObject);
        }
        private void CloseSettings()
        {
            Settings.Show(false, display.DarkMode); app.Menu.SetBlocked(false);
            bool paused = app.Coordinator.Phase == RunPhase.Running && app.Coordinator.Run.IsPaused;
            PauseMenu.Show(paused);
            Focus(paused ? PauseMenu.SettingsButton.gameObject : app.Menu.SettingsButton.gameObject);
        }
        private void Resume()
        {
            if (!app.Coordinator.Resume()) return;
            PauseMenu.Show(false); Settings.Show(false, display.DarkMode); app.Menu.SetBlocked(false); Focus(null);
        }
        private void PhaseChanged(RunPhase phase)
        {
            if (phase == RunPhase.Running) return;
            PauseMenu.Show(false); Settings.Show(false, display.DarkMode); app.Menu.SetBlocked(false); Focus(null);
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
            if (app != null)
            {
                if (app.Menu != null) { app.Menu.SettingsRequested -= OpenSettings; app.Menu.QuitRequested -= Quit; }
                app.Coordinator.PhaseChanged -= PhaseChanged;
            }
            if (PauseMenu != null) { PauseMenu.ResumeRequested -= Resume; PauseMenu.SettingsRequested -= OpenSettings; }
            if (Settings != null) { Settings.BackRequested -= CloseSettings; Settings.DarkModeChanged -= display.SetDarkMode; }
        }
    }
}
