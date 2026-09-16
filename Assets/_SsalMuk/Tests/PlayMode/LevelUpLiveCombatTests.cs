using System.Collections;
using System.IO;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using SsalMuk.Core;
using SsalMuk.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SsalMuk.Tests
{
    public sealed class LevelUpLiveCombatTests
    {
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject);
            yield return null; yield return null;
        }
        [UnityTest]
        public IEnumerator ChoicesRemainOnScreenWhileEnemiesMoveHitAndExperienceArrives()
        {
            yield return SceneManager.LoadSceneAsync(AppRoot.MainMenuScenePath); yield return null;
            var app = AppRoot.Instance; app.Menu.StartButton.onClick.Invoke();
            double deadline = Time.realtimeSinceStartupAsDouble + 20;
            while (app.Coordinator.Phase != RunPhase.Running && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(app.Coordinator.Phase, Is.EqualTo(RunPhase.Running));
            var run = app.Coordinator.Run; new ProgressionService(run).AddExperience(5); yield return new WaitForSeconds(0.08f);
            Assert.That(app.LevelUp, Is.Not.Null); Assert.That(app.LevelUp.IsVisible, Is.True); Assert.That(app.LevelUp.Buttons.Count, Is.EqualTo(3));
            Assert.That(app.LevelUp.Buttons.All(button => button.interactable), Is.True);
            var offer = run.CurrentOffer; double time = run.Clock.ElapsedSeconds; float scale = Time.timeScale;
            var positions = run.Units.Where(x => x.Kind == UnitKind.Normal).ToDictionary(x => x.Id, x => x.Position);
            double health = run.Player.Health;
            var attacker = new UnitDefinition("live-choice-contact", UnitKind.Air, 10000, 6, 0.22, 5, BigInteger.One);
            new AirEnemyFactory(run.World.Units).Spawn(new UnitSpawnRequest(run.Id, UnitKind.Air, run.Player.Position, attacker));
            long orb = run.World.AddExperience(run.Player.Position.Offset(new DVec2(0, 0.8)), 100, run.Clock.ElapsedSeconds);
            yield return new WaitForSeconds(0.5f);
            Assert.That(run.Clock.ElapsedSeconds, Is.GreaterThan(time + 0.2)); Assert.That(Time.timeScale, Is.EqualTo(scale));
            Assert.That(run.Player.Health, Is.LessThan(health)); Assert.That(run.World.TryGetExperience(orb, out _), Is.False);
            Assert.That(run.Units.Any(x => positions.TryGetValue(x.Id, out var old) && old.DistanceTo(x.Position) > 0.1), Is.True);
            Assert.That(run.CurrentOffer, Is.SameAs(offer)); Assert.That(run.Player.Growth.PendingChoices > 1, Is.True);
            foreach (var title in app.LevelUp.Titles) Assert.That(title.cachedTextGenerator.vertexCount, Is.GreaterThan(4));
            Assert.That(app.LevelUp.RemainingText.cachedTextGenerator.vertexCount, Is.GreaterThan(4));
            string folder = Path.Combine(Application.dataPath, "../Logs/Validation/LevelUp", run.Id.ToString("N")); Directory.CreateDirectory(folder);
            string screenshot = Path.Combine(folder, "live-choices.png"); ScreenCapture.CaptureScreenshot(screenshot); yield return null;
            var pending = run.Player.Growth.PendingChoices; var selected = offer.Choices[0];
            app.LevelUp.Buttons[0].onClick.Invoke(); app.LevelUp.Buttons[0].onClick.Invoke();
            yield return new WaitForSeconds(0.06f);
            Assert.That(run.Player.Growth.PendingChoices, Is.EqualTo(pending - 1)); Assert.That(run.CurrentOffer.Id, Is.GreaterThan(offer.Id));
            if (selected.IsUpgrade) Assert.That(run.Player.Weapons.Get(selected.Weapon).GetLevel(selected.UpgradeKind), Is.EqualTo(BigInteger.One));
            else Assert.That(run.Player.Weapons.Owns(selected.Weapon), Is.True);
            while (!File.Exists(screenshot) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(File.Exists(screenshot), Is.True);
        }
    }
}
