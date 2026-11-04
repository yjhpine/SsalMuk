using System;
using SsalMuk.Core;

namespace SsalMuk.Presentation
{
    public sealed class MenuNavigationPresenter : IDisposable
    {
        private readonly RunCoordinator coordinator;
        private readonly IMenuNavigationView view;
        private bool disposed;

        public MenuNavigationPresenter(RunCoordinator coordinator, IMenuNavigationView view)
        {
            this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            coordinator.PhaseChanged += PhaseChanged;
        }

        public void HandleEscape()
        {
            if (disposed) return;
            if (view.IsSettingsVisible) { CloseSettings(); return; }
            if (coordinator.Phase != RunPhase.Running) return;
            if (coordinator.Run.IsPaused) { Resume(); return; }
            if (coordinator.Pause()) { view.ShowPause(true); view.Focus(MenuFocus.PauseResume); }
        }

        public void OpenSettings()
        {
            if (disposed) return;
            bool inMenu = coordinator.Phase == RunPhase.MainMenu;
            if (!inMenu && (coordinator.Phase != RunPhase.Running || !coordinator.Run.IsPaused)) return;
            view.SetMenuBlocked(true); view.ShowPause(false); view.ShowSettings(true);
            view.Focus(MenuFocus.DarkMode);
        }

        public void CloseSettings()
        {
            if (disposed) return;
            view.ShowSettings(false); view.SetMenuBlocked(false);
            bool paused = coordinator.Phase == RunPhase.Running && coordinator.Run.IsPaused;
            view.ShowPause(paused);
            view.Focus(paused ? MenuFocus.PauseSettings : MenuFocus.MainMenuSettings);
        }

        public void Resume()
        {
            if (disposed || !coordinator.Resume()) return;
            ClearMenus();
        }

        private void PhaseChanged(RunPhase phase)
        {
            if (disposed || phase == RunPhase.Running) return;
            ClearMenus();
        }

        private void ClearMenus()
        {
            view.ShowPause(false); view.ShowSettings(false); view.SetMenuBlocked(false); view.Focus(MenuFocus.None);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true; coordinator.PhaseChanged -= PhaseChanged;
        }
    }
}
