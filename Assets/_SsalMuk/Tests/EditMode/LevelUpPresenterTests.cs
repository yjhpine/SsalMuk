using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using SsalMuk.Core;
using SsalMuk.Presentation;

namespace SsalMuk.Tests
{
    public sealed class LevelUpPresenterTests
    {
        [Test]
        public void PresenterQueuesTheDisplayedOfferAndReleasesItsSubscription()
        {
            using var rig = RunTestRig.Create(enableAi: false); rig.GrantExperience(5); rig.Advance(0.02);
            var view = new View(); var presenter = new LevelUpPresenter(view); presenter.Refresh(rig.Run);
            Assert.That(view.Visible, Is.True); Assert.That(view.Titles.Count, Is.EqualTo(3));
            Assert.That(view.Remaining, Does.Contain("1"));
            view.Submit(0); view.Submit(0); presenter.Refresh(rig.Run);
            Assert.That(view.CanSubmit, Is.False); Assert.That(rig.Player.Growth.PendingChoices, Is.EqualTo(BigInteger.One));
            rig.Advance(0.02); presenter.Refresh(rig.Run); Assert.That(view.Visible, Is.False);
            rig.GrantExperience(100); rig.Advance(0.02); presenter.Refresh(rig.Run); presenter.Dispose();
            var before = rig.Player.Growth.PendingChoices; view.Submit(0); rig.Advance(0.02);
            Assert.That(rig.Player.Growth.PendingChoices, Is.EqualTo(before));
        }
        [TestCase(9, "+15%", "10단계까지")]
        [TestCase(10, "+5%", "11단계부터")]
        public void SpeedOfferShowsTheActualNextUpgradeAtTheBoundary(int level, string bonus, string interval)
        {
            for (int seed = 1; seed <= 100; seed++)
            {
                using var rig = RunTestRig.Create(seed: seed, enableAi: false);
                rig.Upgrade(WeaponKind.Sword, UpgradeKind.Speed, level);
                rig.GrantExperience(5); rig.Advance(.02);
                var view = new View(); using var presenter = new LevelUpPresenter(view); presenter.Refresh(rig.Run);
                for (int i = 0; i < rig.CurrentOffer.Choices.Count; i++)
                {
                    var reward = rig.CurrentOffer.Choices[i];
                    Assert.That(view.Titles[i], Does.Not.Contain("연속 공격"));
                    if (reward.IsSharedUpgrade || !reward.IsUpgrade || reward.UpgradeKind != UpgradeKind.Speed) continue;
                    Assert.That(view.Descriptions[i], Does.Contain(bonus).And.Contain(interval));
                    return;
                }
            }
            Assert.Fail("No speed offer was found.");
        }
        private sealed class View : ILevelUpView
        {
            public event Action<int> ChoiceRequested;
            public bool Visible, CanSubmit; public string Remaining; public IReadOnlyList<string> Titles, Descriptions;
            public void Submit(int slot) => ChoiceRequested?.Invoke(slot);
            public void Show(bool visible, bool canSubmit, string remaining, IReadOnlyList<string> titles, IReadOnlyList<string> descriptions)
            { Visible = visible; CanSubmit = canSubmit; Remaining = remaining; Titles = titles; Descriptions = descriptions; }
        }
    }
}
