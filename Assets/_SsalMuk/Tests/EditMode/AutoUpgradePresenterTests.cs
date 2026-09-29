using System;
using System.Collections.Generic;
using NUnit.Framework;
using SsalMuk.Core;
using SsalMuk.Presentation;

namespace SsalMuk.Tests
{
    public sealed class AutoUpgradePresenterTests
    {
        [Test]
        public void UnansweredOfferWaitsFiveSecondsThenQueuesOnlyOneChoice()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            rig.GrantExperience(5); rig.Advance(.02);
            var view = new View(); using var presenter = new LevelUpPresenter(view);
            presenter.Refresh(rig.Run);
            rig.Advance(4.98); presenter.Refresh(rig.Run);
            Assert.That(rig.Run.Rewards.HasQueuedChoice, Is.False);
            rig.Advance(.02); presenter.Refresh(rig.Run);
            Assert.That(rig.Run.Rewards.HasQueuedChoice, Is.True, "An unanswered offer must enter AUTO after five active seconds.");
            Assert.That(view.Remaining, Does.Contain("AUTO"));
            var pending = rig.Player.Growth.PendingChoices;
            presenter.Refresh(rig.Run); presenter.Refresh(rig.Run);
            Assert.That(rig.Player.Growth.PendingChoices, Is.EqualTo(pending), "Choices still apply at the next simulation step.");
            rig.Advance(.02); presenter.Refresh(rig.Run);
            Assert.That(rig.Player.Growth.PendingChoices, Is.EqualTo(pending - 1));
            Assert.That(view.Visible, Is.False);
        }

        [Test]
        public void AutoContinuesAcrossOffersButManualChoiceRestartsTheWait()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            rig.GrantExperience(1000); rig.Advance(.02);
            var view = new View(); using var presenter = new LevelUpPresenter(view);
            presenter.Refresh(rig.Run); rig.Advance(5); presenter.Refresh(rig.Run);
            Assert.That(rig.Run.Rewards.HasQueuedChoice, Is.True);
            rig.Advance(.02); presenter.Refresh(rig.Run);
            Assert.That(rig.Run.Rewards.HasQueuedChoice, Is.True, "AUTO must continue without another five-second wait.");
            rig.Advance(.02);
            // Refreshing normally queues AUTO; a manual event first must stop it.
            view.Submit(0);
            Assert.That(view.Remaining, Does.Not.Contain("AUTO"));
            rig.Advance(.02); presenter.Refresh(rig.Run);
            Assert.That(rig.Run.Rewards.HasQueuedChoice, Is.False);
            rig.Advance(4.98); presenter.Refresh(rig.Run);
            Assert.That(rig.Run.Rewards.HasQueuedChoice, Is.False);
            rig.Advance(.02); presenter.Refresh(rig.Run);
            Assert.That(rig.Run.Rewards.HasQueuedChoice, Is.True);
        }

        [Test]
        public void PauseFreezesTheWaitAndRestartDoesNotInheritAuto()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            rig.GrantExperience(1000); rig.Advance(.02);
            var view = new View(); using var presenter = new LevelUpPresenter(view);
            presenter.Refresh(rig.Run); rig.Advance(4); presenter.Refresh(rig.Run);
            rig.Coordinator.Pause(); rig.Advance(10); presenter.Refresh(rig.Run);
            Assert.That(view.Visible, Is.False); Assert.That(rig.Run.Rewards.HasQueuedChoice, Is.False);
            rig.Coordinator.Resume(); rig.Advance(.98); presenter.Refresh(rig.Run);
            Assert.That(rig.Run.Rewards.HasQueuedChoice, Is.False);
            rig.Advance(.02); presenter.Refresh(rig.Run);
            Assert.That(rig.Run.Rewards.HasQueuedChoice, Is.True);
            rig.Hit(rig.Player.Id, 1000); rig.Advance(.02); presenter.Refresh(rig.Run);
            Assert.That(view.Visible, Is.False);
            rig.RestartAsync().GetAwaiter().GetResult(); rig.GrantExperience(5); rig.Advance(.02);
            presenter.Refresh(rig.Run);
            Assert.That(rig.Run.Rewards.HasQueuedChoice, Is.False, "A new run must start with manual selection.");
        }

        [Test]
        public void AnyClickAtTheDeadlineWinsAndAutoSurvivesAnEmptyQueueUntilCancelled()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            rig.GrantExperience(5); rig.Advance(.02);
            var view = new View(); using var presenter = new LevelUpPresenter(view);
            presenter.Refresh(rig.Run); rig.Advance(5);
            presenter.CancelAutomaticSelection(); presenter.Refresh(rig.Run);
            Assert.That(rig.Run.Rewards.HasQueuedChoice, Is.False);
            rig.Advance(5); presenter.Refresh(rig.Run);
            rig.Advance(.02); presenter.Refresh(rig.Run);
            Assert.That(rig.CurrentOffer, Is.Null); Assert.That(presenter.IsAutomatic, Is.True);
            rig.GrantExperience(100); rig.Advance(.02); presenter.Refresh(rig.Run);
            Assert.That(rig.Run.Rewards.HasQueuedChoice, Is.True, "A later level must retain AUTO.");
            rig.Advance(.02); presenter.CancelAutomaticSelection(); presenter.Refresh(rig.Run);
            Assert.That(presenter.IsAutomatic, Is.False); Assert.That(rig.Run.Rewards.HasQueuedChoice, Is.False);
        }

        [Test]
        public void PresenterQueuesTheHighestPriorityOfferedRewardForEachStartingWeapon()
        {
            foreach (WeaponKind start in Enum.GetValues(typeof(WeaponKind)))
            {
                using var rig = RunTestRig.Create(enableAi: false);
                rig.Coordinator.ReturnToMenuAsync().GetAwaiter().GetResult();
                rig.Coordinator.StartRunAsync(start).GetAwaiter().GetResult();
                rig.GrantExperience(1000); rig.Advance(.02);
                var view = new View(); using var presenter = new LevelUpPresenter(view);
                presenter.Refresh(rig.Run); rig.Advance(5);
                var offer = rig.CurrentOffer;
                var chosen = offer.Choices[AutoUpgradePriority.SelectSlot(offer.Choices, start)];
                presenter.Refresh(rig.Run); rig.Advance(.02);
                if (chosen.IsSharedUpgrade) Assert.That(rig.Player.Upgrades.GetLevel(chosen.SharedKind).IsOne, Is.True);
                else if (chosen.IsUpgrade) Assert.That(rig.Player.Weapons.Get(chosen.Weapon).GetLevel(chosen.UpgradeKind).IsOne, Is.True);
                else Assert.That(rig.Player.Weapons.Owns(chosen.Weapon), Is.True);
            }
        }

        [Test]
        public void DisposedPresenterCannotQueueAutomaticRewards()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            rig.GrantExperience(5); rig.Advance(.02);
            var presenter = new LevelUpPresenter(new View()); presenter.Refresh(rig.Run); presenter.Dispose();
            rig.Advance(5); presenter.Refresh(rig.Run);
            Assert.That(rig.Run.Rewards.HasQueuedChoice, Is.False);
        }

        private sealed class View : ILevelUpView
        {
            public event Action<int> ChoiceRequested;
            public bool Visible; public string Remaining;
            public void Submit(int slot) => ChoiceRequested?.Invoke(slot);
            public void Show(bool visible, bool canSubmit, string remaining, IReadOnlyList<string> titles, IReadOnlyList<string> descriptions)
            { Visible = visible; Remaining = remaining; }
        }
    }
}
