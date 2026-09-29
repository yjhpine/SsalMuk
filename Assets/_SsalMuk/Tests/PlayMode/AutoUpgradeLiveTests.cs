using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SsalMuk.Core;
using SsalMuk.Unity;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SsalMuk.Tests
{
    public sealed class AutoUpgradeLiveTests
    {
        private Mouse mouse, previousMouse;
        private string evidence;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            previousMouse = Mouse.current; mouse = InputSystem.AddDevice<Mouse>(); mouse.MakeCurrent();
            evidence = Path.Combine(Application.dataPath, "../Logs/Validation/AutoUpgrade", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(evidence);
            yield return SceneManager.LoadSceneAsync(AppRoot.MainMenuScenePath); yield return null;
            var app = AppRoot.Instance;
            var start = app.Coordinator.StartRunAsync(WeaponKind.Fireball);
            double end = Time.realtimeSinceStartupAsDouble + 15;
            while (!start.IsCompleted && Time.realtimeSinceStartupAsDouble < end) yield return null;
            Assert.That(app.Coordinator.Phase, Is.EqualTo(RunPhase.Running), app.Coordinator.LastError);
            Assert.That(app.Coordinator.Run.StartingWeapon, Is.EqualTo(WeaponKind.Fireball));
            Assert.That(app.Coordinator.Run.Player.Weapons.Kinds, Is.EqualTo(new[] { WeaponKind.Fireball }));
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject);
            yield return null; yield return null;
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
        }

        [UnityTest]
        public IEnumerator FiveSecondWaitContinuousAutoAndEveryMouseButtonCancelsOutsideCards()
        {
            var app = AppRoot.Instance; var run = app.Coordinator.Run;
            new ProgressionService(run).AddExperience(1000000);
            yield return WaitForOffer(app); yield return new WaitForEndOfFrame();
            long firstOffer = run.CurrentOffer.Id; double shownAt = run.Clock.ElapsedSeconds;
            yield return Capture(app, "countdown.png");
            double deadline = Time.realtimeSinceStartupAsDouble + 12;
            while (run.Clock.ElapsedSeconds - shownAt < 4.8 && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(run.CurrentOffer.Id, Is.EqualTo(firstOffer));
            Assert.That(app.LevelUp.RemainingText.text, Does.Not.Contain("AUTO"));
            while (!app.LevelUp.RemainingText.text.Contains("AUTO") && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(run.Clock.ElapsedSeconds - shownAt, Is.GreaterThanOrEqualTo(4.98));
            Assert.That(app.LevelUp.RemainingText.text, Does.Contain("AUTO"));
            yield return Capture(app, "automatic.png");
            Assert.That(run.CurrentOffer.Id, Is.GreaterThan(firstOffer), "AUTO must consume successive offers.");

            var blank = new Vector2(Screen.width * .5f, Screen.height * .65f);
            foreach (var button in new[] { MouseButton.Left, MouseButton.Right, MouseButton.Middle, MouseButton.Forward, MouseButton.Back })
            {
                mouse.MakeCurrent(); InputSystem.QueueStateEvent(mouse, new MouseState { position = blank }.WithButton(button));
                yield return null; yield return new WaitForEndOfFrame();
                Assert.That(app.LevelUp.RemainingText.text, Does.Not.Contain("AUTO"), button.ToString());
                long cancelledOffer = run.CurrentOffer.Id;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = blank });
                yield return new WaitForSeconds(.12f);
                Assert.That(run.CurrentOffer.Id, Is.EqualTo(cancelledOffer), "A blank click must not choose a card.");
                Assert.That(run.Rewards.HasQueuedChoice, Is.False);
                // Keep every movement/projectile interval when accelerating repeated input cases.
                AdvanceCombat(run, 251); yield return null; yield return new WaitForEndOfFrame();
                Assert.That(app.LevelUp.RemainingText.text, Does.Contain("AUTO"));
            }
        }

        [UnityTest]
        public IEnumerator PausedCountdownAndManualCardClickAtDeadlinePreserveTheSelectedReward()
        {
            var app = AppRoot.Instance; var run = app.Coordinator.Run;
            new ProgressionService(run).AddExperience(1000);
            yield return WaitForOffer(app); yield return new WaitForEndOfFrame();
            var offer = run.CurrentOffer;
            app.Coordinator.Pause(); yield return null; yield return new WaitForEndOfFrame();
            double frozen = run.Clock.ElapsedSeconds;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(run.Clock.ElapsedSeconds, Is.EqualTo(frozen)); Assert.That(app.LevelUp.IsVisible, Is.False);
            Assert.That(run.Rewards.HasQueuedChoice, Is.False); app.Coordinator.Resume();
            yield return null; yield return new WaitForEndOfFrame();
            int automatic = AutoUpgradePriority.SelectSlot(offer.Choices, run.StartingWeapon), manual = (automatic + 1) % 3;
            var reward = offer.Choices[manual];
            var before = RewardLevel(run, reward); var pending = run.Player.Growth.PendingChoices;
            var target = app.LevelUp.Buttons[manual].GetComponent<RectTransform>(); Canvas.ForceUpdateCanvases();
            var point = RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(target.rect.center));
            AdvanceCombat(run, 250);
            mouse.MakeCurrent(); InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left));
            yield return null; yield return new WaitForEndOfFrame();
            Assert.That(app.LevelUp.RemainingText.text, Does.Not.Contain("AUTO"));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null; yield return new WaitForFixedUpdate(); yield return null; yield return new WaitForEndOfFrame();
            Assert.That(run.Player.Growth.PendingChoices, Is.EqualTo(pending - 1));
            Assert.That(RewardLevel(run, reward), Is.EqualTo(before + 1), "The clicked card must win over AUTO priority.");
            Assert.That(run.Rewards.HasQueuedChoice, Is.False);
        }

        private static System.Numerics.BigInteger RewardLevel(RunModel run, RewardId reward)
            => reward.IsSharedUpgrade ? run.Player.Upgrades.GetLevel(reward.SharedKind) : reward.IsUpgrade ?
                run.Player.Weapons.Get(reward.Weapon).GetLevel(reward.UpgradeKind) : (run.Player.Weapons.Owns(reward.Weapon) ? 1 : 0);

        private static void AdvanceCombat(RunModel run, int ticks)
        {
            var simulation = run.Combat as RunSimulation; Assert.That(simulation, Is.Not.Null);
            for (int i = 0; i < ticks; i++) simulation.Step(run.Clock.FixedStep);
            Assert.That(run.Phase, Is.EqualTo(RunPhase.Running));
        }

        private static IEnumerator WaitForOffer(AppRoot app)
        {
            double end = Time.realtimeSinceStartupAsDouble + 5;
            while (!app.LevelUp.IsVisible && Time.realtimeSinceStartupAsDouble < end) yield return null;
            Assert.That(app.LevelUp.IsVisible, Is.True); Assert.That(app.Coordinator.Run.CurrentOffer, Is.Not.Null);
        }

        private IEnumerator Capture(AppRoot app, string name)
        {
            Canvas.ForceUpdateCanvases();
            Assert.That(app.LevelUp.RemainingText.preferredWidth, Is.LessThanOrEqualTo(app.LevelUp.RemainingText.rectTransform.rect.width));
            string path = Path.Combine(evidence, name); ScreenCapture.CaptureScreenshot(path);
            double end = Time.realtimeSinceStartupAsDouble + 5;
            while ((!File.Exists(path) || new FileInfo(path).Length == 0) && Time.realtimeSinceStartupAsDouble < end) yield return null;
            Assert.That(File.Exists(path) && new FileInfo(path).Length > 0, Is.True);
        }
    }
}
