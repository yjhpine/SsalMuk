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
    public sealed class SurroundWaveLiveTests
    {
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject);
            yield return null; yield return null;
        }
        [UnityTest]
        public IEnumerator GameStartUsesZoomedCameraAndItsBoundsForTheTwoMinuteWave()
        {
            yield return SceneManager.LoadSceneAsync(AppRoot.MainMenuScenePath); yield return null;
            var app = AppRoot.Instance; app.Menu.StartButton.onClick.Invoke();
            double until = Time.realtimeSinceStartupAsDouble + 20;
            while (app.Coordinator.Phase != RunPhase.Running && Time.realtimeSinceStartupAsDouble < until) yield return null;
            Assert.That(app.Coordinator.Phase, Is.EqualTo(RunPhase.Running));
            Assert.That(Camera.main.orthographicSize, Is.EqualTo(8));
            var run = app.Coordinator.Run; var simulation = Object.FindAnyObjectByType<BattleRunner>().Simulation;
            Assert.That(simulation.Spawns.SurroundWaveNumber, Is.Zero);
            var oldIds = run.Units.Select(unit => unit.Id).ToArray();
            // Advance only the clock: this is a schedule/wiring test, not two minutes of survival evidence.
            run.Clock.Advance(6000);
            until = Time.realtimeSinceStartupAsDouble + 8;
            while (simulation.Spawns.SurroundWaveSpawnedCount < 80 && Time.realtimeSinceStartupAsDouble < until) yield return new WaitForFixedUpdate();
            Assert.That(simulation.Spawns.SurroundWaveNumber, Is.EqualTo(1));
            Assert.That(simulation.Spawns.SurroundWaveSpawnedCount, Is.EqualTo(80));
            var normal = run.Units.Where(unit => unit.Kind == UnitKind.Normal && !oldIds.Contains(unit.Id)).ToArray();
            Assert.That(normal.Length, Is.GreaterThanOrEqualTo(80));
            var bounds = new WorldRect(run.Player.Position, Camera.main.orthographicSize * Camera.main.aspect, Camera.main.orthographicSize);
            Assert.That(normal.All(unit => !bounds.Contains(unit.Position)), Is.True);
            Assert.That(run.Units.Count(unit => unit.Kind == UnitKind.Normal), Is.LessThanOrEqualTo(500));
        }
    }
}
