using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SsalMuk.Core;
using SsalMuk.Presentation;
using SsalMuk.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SsalMuk.Tests
{
    public sealed class PowerupLiveTests
    {
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject);
            yield return null; yield return null;
        }
        [UnityTest]
        public IEnumerator GameStartShowsFourItemsCollectsEffectsAndOffersSharedUpgrades()
        {
            yield return SceneManager.LoadSceneAsync(AppRoot.MainMenuScenePath); yield return null;
            var app = AppRoot.Instance; app.Menu.StartButton.onClick.Invoke();
            double deadline = Time.realtimeSinceStartupAsDouble + 20;
            while (app.Coordinator.Phase != RunPhase.Running && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(app.Coordinator.Phase, Is.EqualTo(RunPhase.Running));
            var run = app.Coordinator.Run; var runner = Object.FindAnyObjectByType<BattleRunner>(); runner.enabled = false;
            var world = Object.FindAnyObjectByType<WorldView>(); var presenter = new WorldPresenter(world);
            int index = 0;
            foreach (PowerupKind kind in Enum.GetValues(typeof(PowerupKind)))
                run.World.AddItem(kind, run.Player.Position.Offset(new DVec2(-3 + index++ * 2, -3)));
            presenter.Refresh(run); yield return null;
            Assert.That(world.ItemViews.Count(), Is.EqualTo(4));
            Assert.That(world.ItemViews.Select(view => view.Kind).Distinct().Count(), Is.EqualTo(4));
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/Validation/Powerups", run.Id.ToString("N")));
            Directory.CreateDirectory(directory); ScreenCapture.CaptureScreenshot(Path.Combine(directory, "items.png"));
            yield return new WaitForEndOfFrame(); yield return null;
            long far = run.World.AddExperience(run.Player.Position.Offset(new DVec2(800, 0)), 2, run.Clock.ElapsedSeconds);
            foreach (PowerupKind kind in Enum.GetValues(typeof(PowerupKind))) run.World.AddItem(kind, run.Player.Position);
            runner.Simulation.Step(.02); presenter.Refresh(run);
            Assert.That(run.Player.Effects.RangeMultiplier, Is.EqualTo(10));
            Assert.That(run.Player.MoveSpeed, Is.EqualTo(run.Player.Definition.MoveSpeed * 1.5).Within(1e-8));
            Assert.That(run.World.TryGetExperience(far, out var orb), Is.True);
            Assert.That(orb.State, Is.EqualTo(ExperienceState.Attracting));
            var hud = Object.FindAnyObjectByType<HudView>(); new HudPresenter(hud).Refresh(run); yield return null;
            Assert.That(hud.StatusText.text, Does.Contain("무적").And.Contain("범위 ×10").And.Contain("이속 ×1.5"));
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, "active-effects.png"));
            yield return new WaitForEndOfFrame(); yield return null;
            new ProgressionService(run).AddExperience(10000);
            for (int attempt = 0; attempt < 64; attempt++)
            {
                runner.Simulation.Step(.02); yield return null;
                var offer = run.CurrentOffer;
                int slot = offer.Choices.ToList().FindIndex(choice => choice.IsSharedUpgrade);
                if (slot < 0) { run.Commands.TryQueueChoice(run.Id, offer.Id, 0); continue; }
                Assert.That(app.LevelUp.Titles[slot].text, Does.Contain("공용"));
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "shared-choice.png"));
                yield return new WaitForEndOfFrame(); yield return null;
                var kind = offer.Choices[slot].SharedKind; var before = run.Player.Upgrades.GetLevel(kind);
                app.LevelUp.Buttons[slot].onClick.Invoke(); runner.Simulation.Step(.02);
                Assert.That(run.Player.Upgrades.GetLevel(kind), Is.EqualTo(before + 1));
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "shared-applied.png"));
                yield return new WaitForEndOfFrame(); yield return null;
                break;
            }
            Assert.That(Enum.GetValues(typeof(SharedUpgradeKind)).Cast<SharedUpgradeKind>().Any(kind => run.Player.Upgrades.GetLevel(kind) > 0), Is.True);
            TestContext.WriteLine("Powerup live evidence: " + directory);
        }
    }
}
