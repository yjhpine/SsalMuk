using System;
using SsalMuk.Core;

namespace SsalMuk.Presentation
{
    public sealed class ResultsPresenter : IDisposable
    {
        private readonly RunCoordinator coordinator;
        private readonly IResultsView view;
        public ResultsPresenter(RunCoordinator coordinator, IResultsView view)
        {
            this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            view.RestartRequested += Restart; view.MenuRequested += Menu;
            coordinator.PhaseChanged += Refresh; Refresh(coordinator.Phase);
        }
        private async void Restart() { await coordinator.RestartAsync(); }
        private async void Menu() { await coordinator.ReturnToMenuAsync(); }
        private void Refresh(RunPhase phase)
        {
            var result = coordinator.Result;
            bool visible = phase == RunPhase.Results && result != null;
            view.Show(visible, visible, result == null ? "" : HudPresenter.FormatTime(result.SurvivalSeconds),
                result == null ? "" : NumberFormatter.Format(result.KillCount), result == null ? "" : NumberFormatter.Format(result.FinalLevel));
        }
        public void Dispose()
        { view.RestartRequested -= Restart; view.MenuRequested -= Menu; coordinator.PhaseChanged -= Refresh; }
    }
}
