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
            var growth = player.Growth; var nextCost = run.GrowthSettings.CostForLevels(player.Level, 1);
            view.Show(true, player.Health.ToString("0", CultureInfo.InvariantCulture) + " / " + player.Definition.MaxHealth.ToString("0", CultureInfo.InvariantCulture),
                player.Health / player.Definition.MaxHealth, "Lv. " + NumberFormatter.Format(player.Level),
                "경험치 " + NumberFormatter.Format(growth.ExperienceIntoLevel) + " / " + NumberFormatter.Format(nextCost),
                NumberFormatter.Fraction(growth.ExperienceIntoLevel, nextCost), FormatTime(run.Clock.ElapsedSeconds), "처치 " + NumberFormatter.Format(run.Kills));
        }
        public static string FormatTime(double elapsed)
        {
            long seconds = (long)elapsed;
            return (seconds / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (seconds % 60).ToString("00", CultureInfo.InvariantCulture);
        }
    }
}
