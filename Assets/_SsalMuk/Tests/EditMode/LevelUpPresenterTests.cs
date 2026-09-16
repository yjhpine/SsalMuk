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
        private sealed class View : ILevelUpView
        {
            public event Action<int> ChoiceRequested;
            public bool Visible, CanSubmit; public string Remaining; public IReadOnlyList<string> Titles;
            public void Submit(int slot) => ChoiceRequested?.Invoke(slot);
            public void Show(bool visible, bool canSubmit, string remaining, IReadOnlyList<string> titles, IReadOnlyList<string> descriptions)
            { Visible = visible; CanSubmit = canSubmit; Remaining = remaining; Titles = titles; }
        }
    }
}
