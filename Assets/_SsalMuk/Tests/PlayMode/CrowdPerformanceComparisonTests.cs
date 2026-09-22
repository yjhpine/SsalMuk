using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    // A short, rendered fixed-population comparison, not a normal survival/balance test.
    public sealed class CrowdPerformanceComparisonTests
    {
        private RunTestRig rig;
        private GameObject root;
        private int previousVSync, previousTargetRate;
        private bool previousBackground, settingsSaved;

        [UnityTest, Category("Performance")]
        public IEnumerator Population300() => Compare(300);

        [UnityTest, Category("Performance")]
        public IEnumerator Population500() => Compare(500);

        [UnityTest, Category("Performance")]
        public IEnumerator Population1000() => Compare(1000);

        [UnityTest, Category("Performance")]
        public IEnumerator Population1000Combat() => Compare(1000, true);

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root != null)
            {
                var sampler = root.GetComponent<CrowdPerformanceSampler>();
                if (sampler != null) sampler.Stop();
                Object.Destroy(root);
            }
            rig?.Dispose(); rig = null;
            if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject);
            if (settingsSaved)
            {
                QualitySettings.vSyncCount = previousVSync;
                Application.targetFrameRate = previousTargetRate;
                Application.runInBackground = previousBackground;
                settingsSaved = false;
            }
            yield return null; yield return null;
        }

        private IEnumerator Compare(int count, bool combat = false)
        {
            yield return SceneManager.LoadSceneAsync(AppRoot.BattleScenePath);
            if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject);
            yield return null; yield return null;
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.02f).Within(1e-6));
            previousVSync = QualitySettings.vSyncCount;
            previousTargetRate = Application.targetFrameRate;
            previousBackground = Application.runInBackground;
            settingsSaved = true;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1; Application.runInBackground = true;

            var catalog = Resources.Load<GameCatalog>("Bootstrap/GameCatalog");
            var sourceDefinitions = catalog.Defaults.CreateCatalog();
            var definitions = new DefinitionCatalog(sourceDefinitions.Units.Select(d => new UnitDefinition(d.Id, d.Kind,
                combat ? 1000000000 : d.MaxHealth, !combat && d.Kind == UnitKind.Normal ? 1.5 : d.MoveSpeed, d.BodyRadius, d.ContactDamage, d.ExperienceReward,
                d.HurtRadius, d.VisualFootOffset)), Enum.GetValues(typeof(WeaponKind)).Cast<WeaponKind>().Select(sourceDefinitions.GetWeapon));
            rig = RunTestRig.Create(seed: 260917, enableAi: combat, enableCombat: combat, definitions: definitions,
                movementSettings: catalog.Defaults.CreateMovementSettings(), pickupSettings: catalog.Defaults.CreatePickupSettings());
            // Fixed diagnostic normal speed 1.5 preserves the earlier comparison after gameplay speed tuning.
            // Same density and row-major layout; no player input in movement-only cases.
            int columns = (int)Math.Ceiling(Math.Sqrt(count));
            for (int i = 0; i < count; i++)
                rig.Spawn(UnitKind.Normal, new DVec2(3 + (i % columns) * 0.58, (i / columns - columns / 2.0) * 0.58), combat ? 1000000000 : 10);
            if (combat)
            {
                foreach (WeaponKind kind in Enum.GetValues(typeof(WeaponKind))) if (kind != WeaponKind.Sword) rig.Equip(kind);
                for (int i = 0; i < 200; i++) rig.DropXp(new DVec2((i % 20 - 10) * .4, (i / 20 - 5) * .4), 1 + i % 3 * 5);
            }
            for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++) rig.World.GetChunk(new ChunkCoord(x, y));

            root = new GameObject("Crowd performance comparison " + count);
            var cameraObject = new GameObject("Comparison camera", typeof(Camera));
            cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = 24; camera.aspect = 16f / 9;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .14f, .16f);
            var view = root.AddComponent<WorldView>(); view.Initialize(catalog);
            var presenter = new WorldPresenter(view); presenter.Refresh(rig.Run, 1, 60);
            var sampler = root.AddComponent<CrowdPerformanceSampler>(); sampler.Configure(rig, presenter, view, combat);

            double warmupUntil = Time.realtimeSinceStartupAsDouble + 45;
            while (!sampler.Measuring && Time.realtimeSinceStartupAsDouble < warmupUntil) yield return null;
            Assert.That(sampler.Measuring, Is.True, "Warmup did not finish within 45 seconds.");
            double until = Time.realtimeSinceStartupAsDouble + 90;
            while ((sampler.FrameCount < 20 || sampler.StepCount < 350) && Time.realtimeSinceStartupAsDouble < until) yield return null;
            sampler.Stop();
            var report = sampler.Summarize(count, catalog.Defaults.CreateSpawnSettings().NormalPopulationCap);
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/Validation/CrowdPerformance", rig.Run.Id.ToString("N")));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "report.json"), JsonUtility.ToJson(report, true));
            TestContext.WriteLine("Crowd performance evidence: " + directory);

            Assert.That(report.frames, Is.GreaterThanOrEqualTo(20), "Too few rendered samples within the 90 second comparison limit.");
            Assert.That(report.steps, Is.EqualTo(350), "The comparison must include seven complete simulation seconds.");
            Assert.That(report.minimumVisibleUnits, Is.EqualTo(count + 1));
            Assert.That(rig.Run.Units.Count, Is.EqualTo(count + 1));
            Assert.That(report.modelMeanMs, Is.GreaterThan(0));
            Assert.That(report.viewMeanMs, Is.GreaterThan(0));
            Assert.That(report.shadowRenderers, Is.Zero);
            Assert.That(rig.Run.Phase, Is.EqualTo(RunPhase.Running));
            if (combat) Assert.That(report.weaponLaunchCounts.All(value => value > 0), Is.True, "Every baseline weapon must execute.");
        }
    }

    public sealed class CrowdPerformanceSampler : MonoBehaviour
    {
        private RunTestRig rig;
        private WorldPresenter presenter;
        private WorldView view;
        private readonly List<double> models = new List<double>(4096), views = new List<double>(4096), frames = new List<double>(4096);
        private long modelBytes, viewBytes;
        private bool running, recording, allocationCounterSupported, combatEnabled;
        private double previousFrame, measurementStart, warmupStarted, warmupSeconds;
        private int fixedStepsThisFrame, maximumFixedSteps, minimumVisible = int.MaxValue;
        public int FrameCount => frames.Count;
        public int StepCount => models.Count;
        public bool Measuring => recording;
        private int warmupSteps, maximumProjectiles;
        public void Configure(RunTestRig model, WorldPresenter presentation, WorldView worldView, bool combat)
        {
            rig = model; presenter = presentation; view = worldView; running = true; combatEnabled = combat;
            warmupStarted = Time.realtimeSinceStartupAsDouble;
            long before = GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[1024];
            allocationCounterSupported = GC.GetAllocatedBytesForCurrentThread() > before;
            GC.KeepAlive(probe);
        }
        public void BeginMeasurement()
        {
            models.Clear(); views.Clear(); frames.Clear(); modelBytes = viewBytes = 0;
            fixedStepsThisFrame = maximumFixedSteps = 0; minimumVisible = int.MaxValue;
            previousFrame = 0; measurementStart = Time.realtimeSinceStartupAsDouble; recording = true;
        }
        public void Stop() { running = recording = false; }
        private void FixedUpdate()
        {
            if (!running) return;
            if (warmupSteps < 40)
            {
                rig.Advance(.02); warmupSteps++;
                if (warmupSteps == 40) { warmupSeconds = Time.realtimeSinceStartupAsDouble - warmupStarted; BeginMeasurement(); }
                return;
            }
            if (models.Count >= 350) return;
            long bytes = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
            rig.Advance(0.02);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
            if (recording) { models.Add(Milliseconds(Stopwatch.GetTimestamp() - start)); modelBytes += allocated; fixedStepsThisFrame++; }
            if (combatEnabled) maximumProjectiles = Math.Max(maximumProjectiles, rig.Simulation.Projectiles.Count);
        }
        private void LateUpdate()
        {
            if (!running) return;
            double now = Time.realtimeSinceStartupAsDouble;
            long bytes = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
            double alpha = Math.Max(0, Math.Min(1, (Time.timeAsDouble - Time.fixedTimeAsDouble) / 0.02));
            presenter.Refresh(rig.Run, alpha, 60);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
            if (!recording) return;
            views.Add(Milliseconds(Stopwatch.GetTimestamp() - start)); viewBytes += allocated;
            if (previousFrame > 0) frames.Add((now - previousFrame) * 1000);
            previousFrame = now; minimumVisible = Math.Min(minimumVisible, view.VisibleUnitCount);
            maximumFixedSteps = Math.Max(maximumFixedSteps, fixedStepsThisFrame); fixedStepsThisFrame = 0;
        }
        public CrowdPerformanceReport Summarize(int count, int cap)
        {
            var renderers = view.GetComponentsInChildren<Renderer>(true);
            return new CrowdPerformanceReport {
                utc = DateTime.UtcNow.ToString("O"), runId = rig.Run.Id.ToString("N"), unityVersion = Application.unityVersion,
                environment = combatEnabled ? "Unity Editor PlayMode; player AI, four baseline weapons, contact and 200 initial XP orbs; high HP retains the population; no scheduled spawning" :
                    "Unity Editor PlayMode; actual FixedUpdate + WorldPresenter + runtime UnitViews; no combat, player AI, spawning or XP",
                processor = SystemInfo.processorType, graphics = SystemInfo.graphicsDeviceName,
                enemies = count, configuredNormalCap = cap, seed = 260917, width = Screen.width, height = Screen.height,
                warmupWallSeconds = warmupSeconds, warmupSteps = warmupSteps, measurementWallSeconds = Time.realtimeSinceStartupAsDouble - measurementStart,
                requestedFrames = 20, measurementWallLimitSeconds = 90,
                cameraHalfHeight = 24, viewRadius = 60, combat = combatEnabled, maximumProjectiles = maximumProjectiles,
                normalBodyRadius = rig.Run.Definitions.GetUnit(UnitKind.Normal).BodyRadius,
                normalHurtRadius = rig.Run.Definitions.GetUnit(UnitKind.Normal).HurtRadius,
                weaponLaunchCounts = combatEnabled ? rig.Simulation.Weapons.Values.Select(w => w.LaunchCount).ToArray() : Array.Empty<long>(),
                spacing = .58, fixedStepSeconds = .02, maximumAllowedTimestep = Time.maximumDeltaTime,
                vSync = QualitySettings.vSyncCount, targetFrameRate = Application.targetFrameRate,
                steps = models.Count, frames = frames.Count, simulatedSeconds = models.Count * .02,
                minimumVisibleUnits = minimumVisible, maximumFixedStepsPerFrame = maximumFixedSteps,
                shadowRenderers = renderers.Count(r => r.enabled && r.gameObject.activeInHierarchy && (r.name == "GroundShadow" || r.name == "Shadow")),
                enabledRenderers = renderers.Count(r => r.enabled && r.gameObject.activeInHierarchy),
                modelMeanMs = Mean(models), modelP95Ms = Percentile(models, .95), modelMaxMs = Percentile(models, 1),
                viewMeanMs = Mean(views), viewP95Ms = Percentile(views, .95),
                frameMeanMs = Mean(frames), frameP95Ms = Percentile(frames, .95), frameMaxMs = Percentile(frames, 1),
                allocationCounterSupported = allocationCounterSupported,
                modelAllocatedBytesPerStep = !allocationCounterSupported || models.Count == 0 ? -1 : (double)modelBytes / models.Count,
                viewAllocatedBytesPerFrame = !allocationCounterSupported || views.Count == 0 ? -1 : (double)viewBytes / views.Count
            };
        }
        private static double Milliseconds(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
        private static double Mean(List<double> values) => values.Count == 0 ? 0 : values.Sum() / values.Count;
        private static double Percentile(List<double> values, double p)
        { if (values.Count == 0) return 0; var copy = values.ToArray(); Array.Sort(copy); return copy[Math.Min(copy.Length - 1, (int)Math.Ceiling(copy.Length * p) - 1)]; }
    }

    [Serializable]
    public sealed class CrowdPerformanceReport
    {
        public string utc, runId, unityVersion, environment, processor, graphics;
        public bool allocationCounterSupported, combat;
        public long[] weaponLaunchCounts;
        public int warmupSteps, maximumProjectiles;
        public double cameraHalfHeight, viewRadius, normalBodyRadius, normalHurtRadius;
        public int enemies, configuredNormalCap, seed, width, height, vSync, targetFrameRate, steps, frames, requestedFrames;
        public int minimumVisibleUnits, maximumFixedStepsPerFrame, shadowRenderers, enabledRenderers;
        public double warmupWallSeconds, measurementWallSeconds, measurementWallLimitSeconds, simulatedSeconds, spacing, fixedStepSeconds, maximumAllowedTimestep;
        public double modelMeanMs, modelP95Ms, modelMaxMs, viewMeanMs, viewP95Ms, frameMeanMs, frameP95Ms, frameMaxMs;
        public double modelAllocatedBytesPerStep, viewAllocatedBytesPerFrame;
    }
}
