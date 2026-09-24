using System.Collections;
using System.IO;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using SsalMuk.Core;
using SsalMuk.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SsalMuk.Tests
{
    public sealed class DeathRestartTests
    {
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject);
            yield return null; yield return null;
        }
        [UnityTest]
        public IEnumerator MainMenuOffersAllStartingWeaponsAndHorizontalActions()
        {
            yield return SceneManager.LoadSceneAsync(AppRoot.MainMenuScenePath); yield return null;
            var app = AppRoot.Instance;
            foreach (WeaponKind kind in System.Enum.GetValues(typeof(WeaponKind)))
                Assert.That(app.Menu.GetComponentsInChildren<UnityEngine.UI.Button>(true)
                    .SingleOrDefault(button => button.name == "StartWeapon_" + kind), Is.Not.Null, "Missing starting weapon: " + kind);
            var settings = app.Menu.GetComponentsInChildren<UnityEngine.UI.Button>(true).SingleOrDefault(button => button.name == "Settings");
            var quit = app.Menu.GetComponentsInChildren<UnityEngine.UI.Button>(true).SingleOrDefault(button => button.name == "Quit");
            Assert.That(settings, Is.Not.Null); Assert.That(quit, Is.Not.Null);
            Canvas.ForceUpdateCanvases();
            Assert.That(settings.transform.position.x, Is.GreaterThan(app.Menu.StartButton.transform.position.x));
            Assert.That(quit.transform.position.x, Is.GreaterThan(settings.transform.position.x));
            Assert.That(settings.transform.position.y, Is.EqualTo(app.Menu.StartButton.transform.position.y).Within(1));
            Assert.That(quit.transform.position.y, Is.EqualTo(settings.transform.position.y).Within(1));
        }
        [UnityTest]
        public IEnumerator MenuChoiceStartsOnlyThatWeaponAndRestartKeepsChoiceWithFreshGrowth()
        {
            yield return SceneManager.LoadSceneAsync(AppRoot.MainMenuScenePath); yield return null;
            var app = AppRoot.Instance;
            string evidence = Path.Combine(Application.dataPath, "../Logs/Validation/StartingWeapon", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(evidence);
            foreach (WeaponKind kind in System.Enum.GetValues(typeof(WeaponKind)))
            {
                var choice = app.Menu.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(button => button.name == "StartWeapon_" + kind);
                choice.onClick.Invoke(); Assert.That(app.Menu.SelectedWeapon, Is.EqualTo(kind));
                if (kind == WeaponKind.Fireball) yield return Capture(Path.Combine(evidence, "main-menu.png"));
                app.Menu.StartButton.onClick.Invoke(); yield return WaitFor(app, RunPhase.Running);
                var run = app.Coordinator.Run;
                Assert.That(run.Player.Weapons.Kinds, Is.EqualTo(new[] { kind }));
                var runner = Object.FindAnyObjectByType<BattleRunner>(); runner.enabled = false;
                var target = new UnitDefinition("starter-target", UnitKind.Normal, 10000, .01, .26, 0, 1);
                new GroundEnemyFactory(run.World.Units).Spawn(new UnitSpawnRequest(run.Id, UnitKind.Normal, run.Player.Position.Offset(new DVec2(1.4, 0)), target));
                runner.Simulation.Step(1);
                foreach (WeaponKind weapon in System.Enum.GetValues(typeof(WeaponKind)))
                    Assert.That(runner.Simulation.Weapons[weapon].LaunchCount, weapon == kind ? Is.GreaterThan(0) : Is.EqualTo(0), weapon.ToString());
                if (kind == WeaponKind.Fireball)
                {
                    new GrowthService(run).Upgrade(kind, UpgradeKind.Damage, 3);
                    var oldId = run.Id; runner.enabled = true; SpawnContact(run, 1000);
                    yield return WaitFor(app, RunPhase.Results);
                    app.Results.RestartButton.onClick.Invoke(); yield return WaitFor(app, RunPhase.Running);
                    Assert.That(app.Coordinator.Run.Id, Is.Not.EqualTo(oldId));
                    Assert.That(app.Coordinator.Run.Player.Weapons.Kinds, Is.EqualTo(new[] { kind }));
                    Assert.That(app.Coordinator.Run.Player.Weapons.Get(kind).GetLevel(UpgradeKind.Damage), Is.EqualTo(BigInteger.Zero));
                    Assert.That(app.Coordinator.Run.Player.Level, Is.EqualTo(BigInteger.One));
                }
                _ = app.Coordinator.ReturnToMenuAsync(); yield return WaitFor(app, RunPhase.MainMenu);
            }
        }
        [UnityTest]
        public IEnumerator RealContactShowsHudAndResultsThenButtonsStartCleanRuns()
        {
            yield return SceneManager.LoadSceneAsync(AppRoot.MainMenuScenePath); yield return null;
            var app = AppRoot.Instance; app.Menu.StartButton.onClick.Invoke(); yield return WaitFor(app, RunPhase.Running);
            Assert.That(app.Hud, Is.Not.Null); Assert.That(app.Hud.HealthText, Is.Not.Null);
            string evidence = Path.Combine(Application.dataPath, "../Logs/Validation/DeathRestart", app.Coordinator.Run.Id.ToString("N"));
            Directory.CreateDirectory(evidence);
            for (int cycle = 0; cycle < 2; cycle++)
            {
                var run = app.Coordinator.Run; var id = run.Id; yield return new WaitForSeconds(0.12f);
                var firstContact = SpawnContact(run, 5); yield return new WaitForSeconds(0.08f);
                Assert.That(run.Player.Health, Is.LessThan(100));
                Assert.That(app.Hud.HealthText.text, Does.StartWith(run.Player.Health.ToString("0")));
                Assert.That(app.Hud.HealthText.cachedTextGenerator.vertexCount, Is.GreaterThan(4), "The health value must have visible text geometry.");
                Assert.That(app.Hud.LevelText.cachedTextGenerator.vertexCount, Is.GreaterThan(4), "The level must have visible text geometry.");
                if (cycle == 0) yield return Capture(Path.Combine(evidence, "battle-hud.png"));
                // The first bat can linger after a sword knockback and renew invulnerability.
                // Observe its departure before sending the independent lethal contact.
                double clearDeadline = Time.realtimeSinceStartupAsDouble + 5;
                while ((firstContact.Position.DistanceTo(run.Player.Position) < 2 || run.Clock.ElapsedSeconds < run.Player.InvulnerableUntil)
                    && Time.realtimeSinceStartupAsDouble < clearDeadline) yield return new WaitForFixedUpdate();
                Assert.That(firstContact.Position.DistanceTo(run.Player.Position), Is.GreaterThanOrEqualTo(2));
                SpawnContact(run, 1000);
                yield return WaitFor(app, RunPhase.Results); yield return null; yield return null;
                Assert.That(app.Results.Summary.text, Does.Contain("최종 레벨"));
                Assert.That(app.Coordinator.Result.RunId, Is.EqualTo(id)); Assert.That(app.Coordinator.Run, Is.Null);
                Assert.That(Object.FindObjectsByType<UnitView>(FindObjectsSortMode.None), Is.Empty);
                Assert.That(Object.FindObjectsByType<WorldView>(FindObjectsSortMode.None), Is.Empty);
                Assert.That(RunScope.ActiveCount, Is.Zero);
                Assert.That(app.Results.RestartButton.interactable, Is.True);
                if (cycle == 0) yield return Capture(Path.Combine(evidence, "results.png"));
                app.Results.RestartButton.onClick.Invoke(); app.Results.RestartButton.onClick.Invoke();
                yield return WaitFor(app, RunPhase.Running); yield return null;
                Assert.That(app.Coordinator.Run.Id, Is.Not.EqualTo(id));
                Assert.That(app.Coordinator.Run.Player.Weapons.Kinds, Is.EquivalentTo(new[] { WeaponKind.Sword }));
                Assert.That(Object.FindObjectsByType<AppRoot>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
                Assert.That(Object.FindObjectsByType<WorldView>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
                Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
                Assert.That(RunScope.ActiveCount, Is.EqualTo(1));
            }
            SpawnContact(app.Coordinator.Run, 1000); yield return WaitFor(app, RunPhase.Results);
            app.Results.MenuButton.onClick.Invoke(); yield return WaitFor(app, RunPhase.MainMenu);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(AppRoot.MainMenuScenePath));
            Assert.That(app.Menu.StartButton.interactable, Is.True);
            app.Menu.StartButton.onClick.Invoke(); yield return WaitFor(app, RunPhase.Running);
        }
        private static IEnumerator Capture(string path)
        {
            yield return null; ScreenCapture.CaptureScreenshot(path);
            double deadline = Time.realtimeSinceStartupAsDouble + 8;
            while ((!File.Exists(path) || new FileInfo(path).Length == 0) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(File.Exists(path) && new FileInfo(path).Length > 0, Is.True);
        }
        private static UnitModel SpawnContact(RunModel run, double damage)
        {
            var definition = new UnitDefinition("contact-fixture", UnitKind.Air, 10000, 6, 0.26, damage, BigInteger.One);
            return new AirEnemyFactory(run.World.Units).Spawn(new UnitSpawnRequest(run.Id, UnitKind.Air, run.Player.Position, definition));
        }
        private static IEnumerator WaitFor(AppRoot app, RunPhase phase)
        {
            double end = Time.realtimeSinceStartupAsDouble + 20;
            while (app.Coordinator.Phase != phase && Time.realtimeSinceStartupAsDouble < end)
            { Assert.That(app.Coordinator.LastError, Is.Empty); yield return null; }
            Assert.That(app.Coordinator.Phase, Is.EqualTo(phase));
        }
    }
}
