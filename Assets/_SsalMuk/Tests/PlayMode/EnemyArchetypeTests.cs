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
    public sealed class EnemyArchetypeTests
    {
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject);
            yield return null; yield return null;
        }
        [UnityTest]
        public IEnumerator RealAirWaveRendersThreeLongLanes()
        {
            yield return SceneManager.LoadSceneAsync(AppRoot.MainMenuScenePath); yield return null;
            var app = AppRoot.Instance; app.Menu.StartButton.onClick.Invoke();
            double deadline = Time.realtimeSinceStartupAsDouble + 20;
            while (app.Coordinator.Phase != RunPhase.Running && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(app.Coordinator.Phase, Is.EqualTo(RunPhase.Running));
            var run = app.Coordinator.Run; var runner = Object.FindAnyObjectByType<BattleRunner>(); runner.enabled = false;
            var movement = (MovementSystem)typeof(RunSimulation).GetField("movement",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(runner.Simulation);
            movement.SetMoveIntent(run.Player.Id, DVec2.Zero);
            var camera = Camera.main;
            runner.Simulation.Spawns.Tick(20, new WorldRect(run.Player.Position, camera.orthographicSize * camera.aspect, camera.orthographicSize));
            var air = run.Units.OfType<AirEnemyModel>().ToArray(); Assert.That(air.Length, Is.EqualTo(8));
            // Isolate the real spawned formation for visual inspection, then advance its existing movement service.
            foreach (var unit in run.Units.Where(unit => unit.Kind == UnitKind.Normal).ToArray()) run.World.Units.Remove(unit.Id);
            var direction = air[0].OriginalDirection;
            double distance = -air.Average(unit => DVec2.Dot(run.Player.Position.DisplacementTo(unit.Position), direction));
            int steps = (int)System.Math.Round(distance / (air[0].MoveSpeed * .02));
            for (int i = 0; i < steps; i++) movement.Step(.02);
            var view = Object.FindAnyObjectByType<WorldView>(); new WorldPresenter(view).Refresh(run);
            yield return null;
            Assert.That(view.VisibleUnitCount, Is.EqualTo(9));
            var across = new DVec2(-direction.Y, direction.X);
            Assert.That(air.Select(unit => System.Math.Round(DVec2.Dot(air[0].Position.DisplacementTo(unit.Position), across), 6)).Distinct().Count(), Is.EqualTo(3));
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/Validation/AirFormation", run.Id.ToString("N")));
            Directory.CreateDirectory(directory); ScreenCapture.CaptureScreenshot(Path.Combine(directory, "three-lanes.png"));
            yield return new WaitForEndOfFrame();
            TestContext.WriteLine("Air formation screenshot: " + directory);
        }

        [UnityTest]
        public IEnumerator RealBattleConnectsCameraBoundsAndIndependentBossDeadlines()
        {
            yield return SceneManager.LoadSceneAsync(AppRoot.MainMenuScenePath); yield return null;
            var app = AppRoot.Instance; app.Menu.StartButton.onClick.Invoke();
            double deadline = Time.realtimeSinceStartupAsDouble + 20;
            while (app.Coordinator.Phase != RunPhase.Running && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(app.Coordinator.Phase, Is.EqualTo(RunPhase.Running));
            var run = app.Coordinator.Run; var director = Object.FindAnyObjectByType<BattleRunner>().Simulation.Spawns;
            Assert.That(director, Is.Not.Null);
            // Deadline catch-up fixture: this is not evidence of a five-minute performance run.
            run.Clock.Advance(15000);
            yield return WaitForBoss(director, 1);
            Assert.That(director.SpawnedCount(UnitKind.Boss), Is.EqualTo(1));
            // The new surrounding burst shares the creation budget; the air group can follow a few ticks later.
            deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (!run.Units.OfType<AirEnemyModel>().Any() && Time.realtimeSinceStartupAsDouble < deadline) yield return new WaitForFixedUpdate();
            Assert.That(run.Units.OfType<AirEnemyModel>().Any(), Is.True);
            var boss = run.Units.Single(x => x.Kind == UnitKind.Boss); double health = boss.Health;
            Assert.That(run.World.Query.IsCircleFree(boss.Position, boss.BodyRadius), Is.True);
            var camera = Camera.main;
            Assert.That(new WorldRect(run.ViewOrigin, camera.orthographicSize * camera.aspect, camera.orthographicSize).Contains(boss.Position), Is.False);
            var air = run.Units.OfType<AirEnemyModel>().First(); var direction = air.OriginalDirection;
            // Keep the catch-up fixture's hundreds of deferred units away from the next placement band.
            run.World.MoveUnit(run.Player.Id, new WorldPosition(new ChunkCoord(10, 10), new DVec2(16.5, 16.5)));
            run.Clock.Advance(15000); yield return WaitForBoss(director, 2);
            Assert.That(director.SpawnedCount(UnitKind.Boss), Is.EqualTo(2));
            Assert.That(run.Units.Count(x => x.Kind == UnitKind.Boss && x.IsAlive), Is.EqualTo(2));
            Assert.That(boss.Health, Is.EqualTo(health)); Assert.That(air.OriginalDirection, Is.EqualTo(direction));
            Object.Destroy(app.gameObject); yield return null; yield return null;
            Assert.That(director.Pending, Is.Empty); Assert.That(run.Units.Count, Is.Zero);
            director.Tick(900, new WorldRect(default, 16, 9)); Assert.That(run.Units.Count, Is.Zero);
        }
        private static IEnumerator WaitForBoss(SpawnDirector director, int count)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (director.SpawnedCount(UnitKind.Boss) < count && Time.realtimeSinceStartupAsDouble < deadline) yield return new WaitForFixedUpdate();
            Assert.That(director.SpawnedCount(UnitKind.Boss), Is.EqualTo(count), "A blocked placement must keep retrying its ticket.");
        }
    }
}
