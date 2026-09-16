using System;
using System.Numerics;
using SsalMuk.Core;
namespace SsalMuk.Presentation
{
    public sealed class LevelUpPresenter : IDisposable
    {
        private readonly ILevelUpView view;
        private readonly string[] titles = new string[3], descriptions = new string[3];
        private RunModel shownRun;
        private OfferSnapshot shownOffer;
        private BigInteger shownPending = -1;
        private string pendingText = "";
        public LevelUpPresenter(ILevelUpView view)
        { this.view = view ?? throw new ArgumentNullException(nameof(view)); view.ChoiceRequested += Choose; }
        public void Refresh(RunModel run)
        {
            var offer = run?.CurrentOffer;
            if (run == null || run.Phase != RunPhase.Running || offer == null)
            { shownRun = null; shownOffer = null; view.Show(false, false, "", titles, descriptions); return; }
            if (!ReferenceEquals(offer, shownOffer))
            {
                for (int i = 0; i < offer.Choices.Count; i++)
                {
                    var reward = offer.Choices[i];
                    titles[i] = WeaponName(reward.Weapon) + (reward.IsUpgrade ? " " + UpgradeName(reward.UpgradeKind) : " 획득");
                    if (reward.IsUpgrade)
                    {
                        var level = run.Player.Weapons.Get(reward.Weapon).GetLevel(reward.UpgradeKind);
                        descriptions[i] = "단계 " + NumberFormatter.Format(level) + " → " + NumberFormatter.Format(level + 1) + "\n" + UpgradeDescription(reward.UpgradeKind);
                    }
                    else descriptions[i] = "새 무기를 장착합니다\n자동 공격에 추가됩니다";
                }
            }
            shownRun = run; shownOffer = offer;
            if (shownPending != run.Player.Growth.PendingChoices)
            {
                shownPending = run.Player.Growth.PendingChoices;
                pendingText = "남은 선택 " + NumberFormatter.Format(shownPending) + "회  ·  전투 진행 중";
            }
            view.Show(true, !run.Rewards.HasQueuedChoice, pendingText, titles, descriptions);
        }
        private void Choose(int slot)
        {
            if (shownRun == null || shownOffer == null) return;
            shownRun.Commands.TryQueueChoice(shownRun.Id, shownOffer.Id, slot); Refresh(shownRun);
        }
        private static string WeaponName(WeaponKind kind) => kind == WeaponKind.Sword ? "철검" : kind == WeaponKind.Spear ? "철창" : kind == WeaponKind.Axe ? "철도끼" : "파이어볼";
        private static string UpgradeName(UpgradeKind kind) => kind == UpgradeKind.Damage ? "공격력" : kind == UpgradeKind.Copies ? "개수" :
            kind == UpgradeKind.Repeats ? "연속 공격" : kind == UpgradeKind.Speed ? "공격 속도" : "범위";
        private static string UpgradeDescription(UpgradeKind kind) => kind == UpgradeKind.Damage ? "한 번의 공격이 더 강해집니다" : kind == UpgradeKind.Copies ? "왼쪽과 오른쪽에 하나씩 추가" :
            kind == UpgradeKind.Repeats ? "한 묶음의 연속 공격 1회 추가" : kind == UpgradeKind.Speed ? "공격 사이의 대기 시간 감소" : "무기의 공격 범위 증가";
        public void Dispose() { view.ChoiceRequested -= Choose; shownRun = null; shownOffer = null; }
    }
}
