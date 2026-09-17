using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using SsalMuk.Core;
using SsalMuk.Presentation;
using SsalMuk.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Stopwatch = System.Diagnostics.Stopwatch;
using Object = UnityEngine.Object;

namespace SsalMuk.Tests
{
    // Fixed simulation work per rendered frame; this measures cost and retention, not real-time FPS.
    public sealed class FireballPressureTests
    {
        private GameObject root;
        private RunTestRig rig;
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            rig?.Dispose(); rig = null;
            if (root != null) Object.Destroy(root);
            if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject);
            yield return null; yield return null;
        }

        [UnityTest, Category("Performance")]
        public IEnumerator HighGrowthMissedVolleysKeepAllLaunchesAndProduceBoundedEvidence()
        {
            yield return SceneManager.LoadSceneAsync(AppRoot.BattleScenePath);
            if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject);
            yield return null; yield return null;
            var catalog = Resources.Load<GameCatalog>("Bootstrap/GameCatalog");
            rig = RunTestRig.Create(enableAi: false);
            rig.Equip(WeaponKind.Fireball);
            rig.Upgrade(WeaponKind.Fireball, UpgradeKind.Copies, 10);
            rig.Upgrade(WeaponKind.Fireball, UpgradeKind.Repeats, 10);
            rig.Upgrade(WeaponKind.Fireball, UpgradeKind.Speed, 10);
            long target = rig.Spawn(UnitKind.Normal, new DVec2(40, 0), 1000000000);
            rig.Movement.SetMoveIntent(target, DVec2.Zero);
            root = new GameObject("Fireball pressure comparison");
            var cameraObject = new GameObject("Comparison camera", typeof(Camera));
            cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = 10; camera.aspect = 16f / 9;
            var view = root.AddComponent<WorldView>(); view.Initialize(catalog);
            var presenter = new WorldPresenter(view);
            var report = new FireballPressureReport();
            var watch = new Stopwatch();
            for (int i = 0; i < 300; i++)
            {
                rig.Simulation.SetViewBounds(new WorldRect(rig.Player.Position, 160.0 / 9, 10));
                watch.Restart(); rig.Simulation.Step(.02); watch.Stop();
                double modelMs = watch.Elapsed.TotalMilliseconds;
                watch.Restart(); presenter.Refresh(rig.Run, 1, 30); watch.Stop();
                report.peakProjectiles = Math.Max(report.peakProjectiles, rig.Simulation.Projectiles.Count);
                if (i >= 150)
                {
                    report.samples++; report.meanModelMs += modelMs; report.meanViewMs += watch.Elapsed.TotalMilliseconds;
                    report.meanProjectiles += rig.Simulation.Projectiles.Count;
                }
                yield return null;
            }
            report.meanModelMs /= report.samples; report.meanViewMs /= report.samples; report.meanProjectiles /= report.samples;
            report.launches = rig.Simulation.Weapons[WeaponKind.Fireball].LaunchCount;
            report.finalProjectiles = rig.Simulation.Projectiles.Count;
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/Validation/FireballPressure", rig.Run.Id.ToString("N")));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "report.json"), JsonUtility.ToJson(report, true));
            TestContext.WriteLine("Fireball pressure evidence: " + directory);
            Assert.That(report.samples, Is.EqualTo(150));
            Assert.That(report.launches, Is.EqualTo(2058));
            Assert.That(rig.Unit(target).Health, Is.EqualTo(1000000000));
            Assert.That(rig.World.Units.Count, Is.EqualTo(2));
            var projectiles = rig.Simulation.Projectiles;
            rig.Dispose(); rig = null;
            Assert.That(projectiles, Is.Empty);
        }
    }

    [Serializable]
    public sealed class FireballPressureReport
    {
        public string scope = "6 simulation seconds, 300 rendered frames, last 150 measured; 21 copies, 11 repeats, speed +10; stationary player and distant target; no AI/spawning/XP; not FPS evidence";
        public int samples, peakProjectiles, finalProjectiles;
        public long launches;
        public double meanModelMs, meanViewMs, meanProjectiles;
    }
}
