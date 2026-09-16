using System;
using SsalMuk.Core;

namespace SsalMuk.Presentation
{
    public sealed class MainMenuPresenter : IDisposable
    {
        private readonly RunCoordinator coordinator;
        private readonly IMainMenuView view;
        public MainMenuPresenter(RunCoordinator coordinator, IMainMenuView view)
        {
            this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            view.StartRequested += Start; coordinator.PhaseChanged += Refresh; Refresh(coordinator.Phase);
        }
        private async void Start() { await coordinator.StartRunAsync(); }
        private void Refresh(RunPhase phase)
        {
            bool menu = phase == RunPhase.MainMenu;
            view.Show(phase != RunPhase.Running && phase != RunPhase.Results && phase != RunPhase.Disposed, menu,
                !string.IsNullOrEmpty(coordinator.LastError) ? "시작 준비에 실패했습니다. 다시 시도해 주세요." : menu ? "준비가 되면 시작하세요." : "월드를 준비하고 있습니다…");
        }
        public void Dispose() { view.StartRequested -= Start; coordinator.PhaseChanged -= Refresh; }
    }
}
