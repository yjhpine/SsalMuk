using System;
using System.Globalization;
using System.Linq;
using SsalMuk.Core;

namespace SsalMuk.Presentation
{
    public sealed class HudPresenter
    {
        private readonly IHudView view;
        private Guid shownRun;
        private long shownOwnership = -1;
        private string weapons = "";
        public HudPresenter(IHudView view) => this.view = view ?? throw new ArgumentNullException(nameof(view));
        public void Refresh(RunModel run)
        {
            if (run == null || run.Phase != RunPhase.Running || run.Player == null)
            { view.Show(false, "", 0, "", "", 0, "", "", ""); return; }
            var player = run.Player;
            if (view is IStatusHudView statusView)
            {
                double now = run.Clock.ElapsedSeconds; var effects = player.Effects;
                string active = "";
                if (effects.InvincibilityUntil > now) active += "무적 " + Math.Ceiling(effects.InvincibilityUntil - now) + "초   ";
                if (effects.SpeedUntil > now) active += "이속 ×1.5 " + Math.Ceiling(effects.SpeedUntil - now) + "초   ";
                if (effects.RangeUntil > now) active += "범위 ×10 " + Math.Ceiling(effects.RangeUntil - now) + "초";
                string upgrades = "이속 +" + NumberFormatter.Format(player.Upgrades.GetLevel(SharedUpgradeKind.MoveSpeed) * 10) +
                    "% · 회복 " + player.Upgrades.HealingPerSecond.ToString("0.0", CultureInfo.InvariantCulture) + "/초 · 흡수 +" +
                    NumberFormatter.Format(player.Upgrades.GetLevel(SharedUpgradeKind.PickupRange) * 20) + "% · 경험치 +" +
                    NumberFormatter.Format(player.Upgrades.GetLevel(SharedUpgradeKind.Experience) * 25) + "%";
                statusView.ShowStatus(upgrades + (active.Length > 0 ? "\n" + active : ""));
            }
            if (shownRun != run.Id || shownOwnership != player.Weapons.OwnershipVersion)
            {
                shownRun = run.Id; shownOwnership = player.Weapons.OwnershipVersion;
                weapons = string.Join("  ·  ", player.Weapons.Kinds.Select(kind => kind == WeaponKind.Sword ? "철검" :
                    kind == WeaponKind.Spear ? "철창" : kind == WeaponKind.Axe ? "철도끼" : "파이어볼"));
            }
            var growth = player.Growth; var nextCost = run.GrowthSettings.CostForLevels(player.Level, 1);
            view.Show(true, player.Health.ToString("0", CultureInfo.InvariantCulture) + " / " + player.Definition.MaxHealth.ToString("0", CultureInfo.InvariantCulture),
                player.Health / player.Definition.MaxHealth, "Lv. " + NumberFormatter.Format(player.Level),
                "경험치 " + NumberFormatter.Format(growth.ExperienceIntoLevel) + " / " + NumberFormatter.Format(nextCost),
                NumberFormatter.Fraction(growth.ExperienceIntoLevel, nextCost), FormatTime(run.Clock.ElapsedSeconds), "처치 " + NumberFormatter.Format(run.Kills), weapons);
        }
        public static string FormatTime(double elapsed)
        {
            long seconds = (long)elapsed;
            return (seconds / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (seconds % 60).ToString("00", CultureInfo.InvariantCulture);
        }
    }
}
