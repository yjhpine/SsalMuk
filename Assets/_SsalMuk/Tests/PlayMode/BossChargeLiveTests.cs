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
        public IEnumerator GameStartRendersSoftCrowdAndBossPassingThroughIt()
        {
            yield return SceneManager.LoadSceneAsync(AppRoot.MainMenuScenePath); yield return null;
            var app = AppRoot.Instance; app.Menu.StartButton.onClick.Invoke();
            double until = Time.realtimeSinceStartupAsDouble + 20;
            while (app.Coordinator.Phase != RunPhase.Running && Time.realtimeSinceStartupAsDouble < until) yield return null;
            Assert.That(app.Coordinator.Phase, Is.EqualTo(RunPhase.Running));
            var run = app.Coordinator.Run; var runner = Object.FindAnyObjectByType<BattleRunner>(); runner.enabled = false;
            var movement = (MovementSystem)typeof(RunSimulation).GetField("movement",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(runner.Simulation);
            foreach (var unit in run.Units.Where(unit => unit.Kind != UnitKind.Player).ToArray()) run.World.Units.Remove(unit.Id);
            movement.SetMoveIntent(run.Player.Id, DVec2.Zero);
            var factory = new GroundEnemyFactory(run.World.Units);
            var boss = factory.Spawn(new UnitSpawnRequest(run.Id, UnitKind.Boss,
                run.Player.Position.Offset(new DVec2(4, 0)), run.Definitions.GetUnit(UnitKind.Boss)));
            var normals = new UnitModel[64];
            for (int i = 0; i < normals.Length; i++)
            {
                var position = run.Player.Position.Offset(new DVec2(1.5 + (i % 8 - 3.5) * .21, (i / 8 - 3.5) * .21));
                normals[i] = factory.Spawn(new UnitSpawnRequest(run.Id, UnitKind.Normal, position, run.Definitions.GetUnit(UnitKind.Normal)));
                movement.SetMoveIntent(normals[i].Id, DVec2.Zero);
            }
            var worldView = Object.FindAnyObjectByType<WorldView>(); var presenter = new WorldPresenter(worldView);
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/Validation/BossCrowd", run.Id.ToString("N")));
            Directory.CreateDirectory(directory);
            for (int i = 0; i < 25; i++) { movement.Step(.02); presenter.Refresh(run); yield return null; }
            Assert.That(worldView.VisibleUnitCount, Is.EqualTo(66));
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, "before-charge.png"));
            yield return new WaitForEndOfFrame();
            var before = boss.Position;
            for (int i = 0; i < 15; i++) { movement.Step(.02); presenter.Refresh(run); yield return null; }
            double chargeSpeed = System.Math.Round(run.Player.MoveSpeed * BossCharge.SpeedMultiplier, 1, System.MidpointRounding.AwayFromZero);
            Assert.That(before.DisplacementTo(boss.Position).X, Is.EqualTo(-chargeSpeed * .3).Within(1e-8));
            Assert.That(movement.GetEnemyFsm(boss.Id).CurrentState, Is.EqualTo(EnemyState.Charge));
            Assert.That(normals.All(unit => unit.Health == unit.Definition.MaxHealth && !unit.Knockback.IsActive), Is.True);
            foreach (var unit in run.Units) Assert.That(run.World.Query.IsCircleFree(unit.Position, unit.BodyRadius), Is.True);
            Assert.That(worldView.GetComponentsInChildren<Collider2D>(true), Is.Empty);
            Assert.That(worldView.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, "after-charge.png"));
            yield return new WaitForEndOfFrame();
            TestContext.WriteLine("Boss crowd screenshots: " + directory);
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
            var damage = (DamageService)typeof(RunSimulation).GetField("damage",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(runner.Simulation);
            Assert.That(boss.Definition.ContactDamage, Is.EqualTo(60));
            double health = boss.Health;
            for (int i = 0; i < 2; i++)
            {
                Assert.That(damage.TryApply(new DamageRequest(new HitKey(run.Id, run.AllocateAttackId(), 0, 0),
                    run.Player.Id, boss.Id, 5, new DVec2(1, 0)), run.Clock.ElapsedSeconds), Is.True);
                Assert.That(boss.Knockback.IsActive, Is.False);
                presenter.Refresh(run); Assert.That(warning.enabled, Is.True);
                movement.Step(.24); presenter.Refresh(run);
            }
            Assert.That(warning.enabled, Is.False);
            Assert.That(damage.TryApply(new DamageRequest(new HitKey(run.Id, run.AllocateAttackId(), 0, 0),
                run.Player.Id, boss.Id, 7, new DVec2(1, 0)), run.Clock.ElapsedSeconds), Is.True);
            Assert.That(boss.Health, Is.EqualTo(health - 17));
            Assert.That(boss.HitSequence, Is.EqualTo(3));
            Assert.That(boss.Knockback.IsActive, Is.False);
            var start = boss.Position; movement.Step(.1);
            Assert.That(start.DisplacementTo(boss.Position).X, Is.EqualTo(-1.56).Within(1e-8));
            Object.Destroy(app.gameObject); yield return null; yield return null;
            Assert.That(warning == null || !warning.enabled, Is.True);
        }
    }
}
