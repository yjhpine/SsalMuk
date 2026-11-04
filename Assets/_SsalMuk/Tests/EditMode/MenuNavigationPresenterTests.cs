using System;
using System.Threading.Tasks;
using NUnit.Framework;
using SsalMuk.Core;
using SsalMuk.Presentation;

namespace SsalMuk.Tests
{
    public sealed class MenuNavigationPresenterTests
    {
        [Test]
        public async Task MainMenuSettingsBlockTheMenuAndReturnFocusWithoutStartingARun()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            await rig.Coordinator.ReturnToMenuAsync();
            var view = new View();
            using var presenter = new MenuNavigationPresenter(rig.Coordinator, view);
            presenter.HandleEscape();
            Assert.That(view.Changes, Is.Zero, "Construction and menu ESC must not refresh the UI.");
            presenter.OpenSettings();
            Assert.That(view.IsSettingsVisible, Is.True);
            Assert.That(view.MenuBlocked, Is.True);
            Assert.That(view.PauseVisible, Is.False);
            Assert.That(view.Selected, Is.EqualTo(MenuFocus.DarkMode));
            presenter.CloseSettings();
            Assert.That(view.IsSettingsVisible, Is.False);
            Assert.That(view.MenuBlocked, Is.False);
            Assert.That(view.PauseVisible, Is.False);
            Assert.That(view.Selected, Is.EqualTo(MenuFocus.MainMenuSettings));
            Assert.That(rig.Coordinator.Phase, Is.EqualTo(RunPhase.MainMenu));
            Assert.That(rig.Coordinator.Run, Is.Null);
        }

        [Test]
        public void EscapePausesAndResumesTheActualRunClock()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            var view = new View();
            using var presenter = new MenuNavigationPresenter(rig.Coordinator, view);
            presenter.HandleEscape();
            Assert.That(rig.Run.IsPaused, Is.True);
            Assert.That(rig.Clock.IsRunning, Is.False);
            Assert.That(view.PauseVisible, Is.True);
            Assert.That(view.Selected, Is.EqualTo(MenuFocus.PauseResume));
            double time = rig.Clock.ElapsedSeconds;
            rig.Advance(.04);
            Assert.That(rig.Clock.ElapsedSeconds, Is.EqualTo(time));
            presenter.HandleEscape();
            Assert.That(rig.Run.IsPaused, Is.False);
            Assert.That(rig.Clock.IsRunning, Is.True);
            Assert.That(view.PauseVisible, Is.False);
            Assert.That(view.IsSettingsVisible, Is.False);
            Assert.That(view.MenuBlocked, Is.False);
            Assert.That(view.Selected, Is.EqualTo(MenuFocus.None));
            rig.Advance(.02);
            Assert.That(rig.Clock.ElapsedSeconds, Is.EqualTo(time + .02).Within(1e-9));
        }

        [Test]
        public void EscapeFromSettingsReturnsToPauseWithoutResumingCombat()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            var view = new View();
            using var presenter = new MenuNavigationPresenter(rig.Coordinator, view);
            presenter.HandleEscape(); presenter.OpenSettings();
            Assert.That(view.IsSettingsVisible, Is.True);
            Assert.That(view.MenuBlocked, Is.True);
            Assert.That(view.PauseVisible, Is.False);
            presenter.HandleEscape();
            Assert.That(view.IsSettingsVisible, Is.False);
            Assert.That(view.MenuBlocked, Is.False);
            Assert.That(view.PauseVisible, Is.True);
            Assert.That(view.Selected, Is.EqualTo(MenuFocus.PauseSettings));
            Assert.That(rig.Run.IsPaused, Is.True);
            Assert.That(rig.Clock.IsRunning, Is.False);
        }

        [Test]
        public void ActiveCombatRejectsSettingsAndRedundantResume()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            var view = new View();
            using var presenter = new MenuNavigationPresenter(rig.Coordinator, view);
            presenter.OpenSettings(); presenter.Resume();
            Assert.That(view.Changes, Is.Zero);
            Assert.That(rig.Run.IsPaused, Is.False);
            Assert.That(rig.Clock.IsRunning, Is.True);
        }

        [Test]
        public async Task LeavingAPausedRunClearsTheOverlayAndAllowsMenuSettings()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            var view = new View();
            using var presenter = new MenuNavigationPresenter(rig.Coordinator, view);
            presenter.HandleEscape(); presenter.OpenSettings();
            await rig.Coordinator.ReturnToMenuAsync();
            Assert.That(view.PauseVisible, Is.False);
            Assert.That(view.IsSettingsVisible, Is.False);
            Assert.That(view.MenuBlocked, Is.False);
            Assert.That(view.Selected, Is.EqualTo(MenuFocus.None));
            presenter.OpenSettings(); presenter.HandleEscape();
            Assert.That(view.Selected, Is.EqualTo(MenuFocus.MainMenuSettings));
            Assert.That(rig.Coordinator.Phase, Is.EqualTo(RunPhase.MainMenu));
        }

        [Test]
        public async Task LoadingRejectsMenuCommandsAndFailureReturnsToUsableMenu()
        {
            var loader = new GateLoader();
            using var rig = RunTestRig.Create(enableAi: false, sceneLoader: loader);
            await rig.Coordinator.ReturnToMenuAsync();
            var view = new View();
            using var presenter = new MenuNavigationPresenter(rig.Coordinator, view);
            presenter.OpenSettings();
            loader.BlockBattle = true;
            Task start = rig.Coordinator.StartRunAsync();
            Assert.That(rig.Coordinator.Phase, Is.EqualTo(RunPhase.Loading));
            Assert.That(view.IsSettingsVisible, Is.False);
            Assert.That(view.MenuBlocked, Is.False);
            Assert.That(view.Selected, Is.EqualTo(MenuFocus.None));
            int changes = view.Changes;
            presenter.HandleEscape(); presenter.OpenSettings(); presenter.Resume();
            Assert.That(view.Changes, Is.EqualTo(changes));
            loader.Fail(); await start;
            Assert.That(rig.Coordinator.Phase, Is.EqualTo(RunPhase.MainMenu));
            Assert.That(rig.Coordinator.LastError, Is.Not.Empty);
            presenter.OpenSettings();
            Assert.That(view.IsSettingsVisible, Is.True);
            Assert.That(view.Selected, Is.EqualTo(MenuFocus.DarkMode));
        }

        [Test]
        public void ResultsClearOverlaysAndRejectSettingsAndEscape()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            var view = new View();
            using var presenter = new MenuNavigationPresenter(rig.Coordinator, view);
            Assert.That(rig.Hit(rig.Player.Id, 1000), Is.True);
            rig.Advance(.02);
            Assert.That(rig.Coordinator.Phase, Is.EqualTo(RunPhase.Results));
            Assert.That(view.IsSettingsVisible, Is.False);
            Assert.That(view.PauseVisible, Is.False);
            Assert.That(view.MenuBlocked, Is.False);
            Assert.That(view.Selected, Is.EqualTo(MenuFocus.None));
            int changes = view.Changes;
            presenter.HandleEscape(); presenter.OpenSettings(); presenter.Resume();
            Assert.That(view.Changes, Is.EqualTo(changes));
        }

        [Test]
        public async Task DisposalDetachesPhaseNotificationsAndIgnoresLateCommands()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            var view = new View();
            var presenter = new MenuNavigationPresenter(rig.Coordinator, view);
            presenter.HandleEscape(); presenter.OpenSettings();
            presenter.Dispose(); presenter.Dispose();
            int changes = view.Changes;
            presenter.HandleEscape(); presenter.OpenSettings(); presenter.CloseSettings(); presenter.Resume();
            Assert.That(rig.Run.IsPaused, Is.True);
            await rig.Coordinator.ReturnToMenuAsync();
            Assert.That(view.Changes, Is.EqualTo(changes));
        }

        private sealed class View : IMenuNavigationView
        {
            public bool IsSettingsVisible { get; private set; }
            public bool MenuBlocked, PauseVisible;
            public MenuFocus Selected;
            public int Changes;
            public void SetMenuBlocked(bool blocked) { MenuBlocked = blocked; Changes++; }
            public void ShowPause(bool visible) { PauseVisible = visible; Changes++; }
            public void ShowSettings(bool visible) { IsSettingsVisible = visible; Changes++; }
            public void Focus(MenuFocus target) { Selected = target; Changes++; }
        }

        private sealed class GateLoader : ISceneLoader
        {
            private readonly TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>();
            public bool BlockBattle;
            public Task LoadBattleAsync() => BlockBattle ? completion.Task : Task.CompletedTask;
            public Task LoadMainMenuAsync() => Task.CompletedTask;
            public void Fail() => completion.TrySetException(new InvalidOperationException("Scene load failed."));
        }
    }
}
