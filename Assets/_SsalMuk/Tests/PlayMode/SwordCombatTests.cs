using System.Collections;
using System.Linq;
using NUnit.Framework;
using SsalMuk.Core;
using SsalMuk.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SsalMuk.Tests
{
    public sealed class SwordCombatTests
    {
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject);
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator GameStartRunsAiAndDealsRealSwordDamageWithoutPlayerInput()
        {
            yield return SceneManager.LoadSceneAsync(AppRoot.MainMenuScenePath); yield return null;
            var app = AppRoot.Instance; app.Menu.StartButton.onClick.Invoke();
            double deadline = Time.realtimeSinceStartupAsDouble + 20;
            while (app.Coordinator.Phase != RunPhase.Running && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(app.Coordinator.Phase, Is.EqualTo(RunPhase.Running));
            var run = app.Coordinator.Run;
            var enemy = run.Units.First(unit => unit.Kind == UnitKind.Normal);
            var position = run.Player.Position; double health = enemy.Health;
            run.World.MoveUnit(enemy.Id, position.Offset(new DVec2(1.2, 0)));
            deadline = Time.realtimeSinceStartupAsDouble + 8;
            while (enemy.Health == health && run.Player.IsAlive && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(enemy.Health, Is.LessThan(health)); Assert.That(enemy.HitSequence, Is.GreaterThan(0));
            Assert.That(run.Player.Position.DistanceTo(position), Is.GreaterThan(0.01));
            Assert.That(run.Clock.ElapsedSeconds, Is.GreaterThan(0));
        }
    }
}
