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

namespace SsalMuk.Tests
{
    public sealed class BossChargeLiveTests
    {
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject);
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator GameStartShowsALockedRedChargeStripAndClearsItWhenCharging()
        {
            yield return SceneManager.LoadSceneAsync(AppRoot.MainMenuScenePath); yield return null;
            var app = AppRoot.Instance; app.Menu.StartButton.onClick.Invoke();
            double until = Time.realtimeSinceStartupAsDouble + 20;
            while (app.Coordinator.Phase != RunPhase.Running && Time.realtimeSinceStartupAsDouble < until) yield return null;
            Assert.That(app.Coordinator.Phase, Is.EqualTo(RunPhase.Running));
            var run = app.Coordinator.Run; var runner = Object.FindAnyObjectByType<BattleRunner>(); runner.enabled = false;
            // Access the existing runtime service without adding a test-only production API.
            var movement = (MovementSystem)typeof(RunSimulation).GetField("movement",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(runner.Simulation);
            movement.SetMoveIntent(run.Player.Id, DVec2.Zero);
            var boss = new GroundEnemyFactory(run.World.Units).Spawn(new UnitSpawnRequest(run.Id, UnitKind.Boss,
                run.Player.Position.Offset(new DVec2(4, 0)), run.Definitions.GetUnit(UnitKind.Boss)));
            movement.Step(.02);
            var worldView = Object.FindAnyObjectByType<WorldView>(); var presenter = new WorldPresenter(worldView); presenter.Refresh(run);
            yield return null;
            var unitView = worldView.UnitViews.Single(view => view.UnitId == boss.Id);
            var warning = unitView.GetComponentsInChildren<SpriteRenderer>().Single(sprite => sprite.name == "BossChargeWarning");
            Assert.That(warning.enabled, Is.True);
            Assert.That(warning.color.r, Is.GreaterThan(.9)); Assert.That(warning.color.g, Is.LessThan(.1));
            Assert.That(warning.transform.localScale.x * warning.sprite.bounds.size.x, Is.EqualTo(8).Within(1e-5));
            Assert.That(warning.transform.localScale.y * warning.sprite.bounds.size.y, Is.EqualTo(boss.BodyRadius * 2).Within(1e-5));
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/Validation/BossCharge", run.Id.ToString("N")));
            Directory.CreateDirectory(directory); ScreenCapture.CaptureScreenshot(Path.Combine(directory, "telegraph.png"));
            yield return new WaitForEndOfFrame();
            TestContext.WriteLine("Boss charge screenshot: " + directory);
            movement.Step(.48); presenter.Refresh(run);
            Assert.That(warning.enabled, Is.False);
            var start = boss.Position; movement.Step(.1);
            Assert.That(start.DisplacementTo(boss.Position).X, Is.EqualTo(-run.Player.Definition.MoveSpeed * .2).Within(1e-8));
            Object.Destroy(app.gameObject); yield return null; yield return null;
            Assert.That(warning == null || !warning.enabled, Is.True);
        }
    }
}
