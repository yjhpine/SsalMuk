using System;
using System.Globalization;
using SsalMuk.Core;

namespace SsalMuk.Presentation
{
    public sealed class HudPresenter
    {
        private readonly IHudView view;
        public HudPresenter(IHudView view) => this.view = view ?? throw new ArgumentNullException(nameof(view));
        public void Refresh(RunModel run)
        {
            if (run == null || run.Phase != RunPhase.Running || run.Player == null)
            { view.Show(false, "", 0, "", "", 0, "", ""); return; }
            var player = run.Player;
            view.Show(true, player.Health.ToString("0", CultureInfo.InvariantCulture) + " / " + player.Definition.MaxHealth.ToString("0", CultureInfo.InvariantCulture),
                player.Health / player.Definition.MaxHealth, "Lv. " + player.Level, "경험치 0", 0, FormatTime(run.Clock.ElapsedSeconds), "처치 " + run.Kills);
        }
        public static string FormatTime(double elapsed)
        {
            long seconds = (long)elapsed;
            return (seconds / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (seconds % 60).ToString("00", CultureInfo.InvariantCulture);
        }
    }
}
