using System;
using SsalMuk.Core;
using UnityEngine;
namespace SsalMuk.Unity
{
    public sealed class ItemUiController : MonoBehaviour
    {
        private Canvas canvas;
        private RunModel current;
        private UpgradeSummaryView upgrades;
        public InventoryView Inventory { get; private set; }
        public FusionView Fusion { get; private set; }
        public ItemRewardView Rewards { get; private set; }
        public ItemTooltip Tooltip { get; private set; }
        public void Initialize(Font font, Action mouseClick)
        {
            canvas = UiFactory.Canvas(transform, "ItemHud", 50); Tooltip = new ItemTooltip(transform, font);
            Fusion = new FusionView(transform, font, Tooltip, () => Refresh(current), mouseClick);
            Inventory = new InventoryView(canvas.transform, font, Tooltip, id => Fusion.Toggle(id), () => Fusion.ToggleOpen());
            Rewards = new ItemRewardView(transform, font, Tooltip, mouseClick);
            upgrades = new UpgradeSummaryView(canvas.transform, font); canvas.gameObject.SetActive(false);
        }
        public void Refresh(RunModel run)
        {
            if (canvas == null) return; current = run;
            bool active = run != null && run.Phase == RunPhase.Running && run.Player != null;
            bool visible = active && !run.IsPaused;
            canvas.gameObject.SetActive(visible); Rewards.Refresh(active ? run : null);
            bool reward = active && (run.CurrentOffer != null || run.HasItemChoice);
            Fusion.Refresh(active ? run : null, visible && !reward);
            if (!visible) { Tooltip.Hide(); return; }
            Inventory.Refresh(run, Fusion.SelectedIds, Fusion.SelectionRevision, !reward, Fusion.IsVisible);
            upgrades.Refresh(run);
        }
    }
}
