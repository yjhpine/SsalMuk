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
    public sealed class CrowdPreviewTests
    {
        private RunTestRig rig;
        private GameObject previewRoot;

        [UnityTest] public IEnumerator OpenSpaceCrowdRemainsStableWithRealViews() => Preview("open");
        [UnityTest] public IEnumerator NarrowPassageCrowdRemainsStableWithRealViews() => Preview("narrow");
        [UnityTest] public IEnumerator WallSideCrowdRemainsStableWithRealViews() => Preview("wall");

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            rig?.Dispose(); rig = null;
            if (previewRoot != null) Object.Destroy(previewRoot);
            if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject);
            yield return null;
            yield return null;
        }

        private IEnumerator Preview(string layout)
        {
            yield return SceneManager.LoadSceneAsync(AppRoot.MainMenuScenePath);
            yield return null;
            Assert.That(AppRoot.Instance, Is.Not.Null);
            AppRoot.Instance.Coordinator.Dispose();
            rig = RunTestRig.Create(terrain: new PreviewTerrain(layout), enableAi: false, enableCombat: false);
            rig.PlacePlayer(layout == "wall" ? new DVec2(15.4, 23) : new DVec2(22, 16.5));
            for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++) rig.World.GetChunk(new ChunkCoord(x, y));
            for (int i = 0; i < 12; i++)
            {
                DVec2 position = layout == "narrow" ? new DVec2(8 + i * 0.57, 16.5) :
                    layout == "wall" ? new DVec2(13.7 + (i % 4) * 0.55, 12 + (i / 4) * 0.55) :
                    new DVec2(12 + (i % 4) * 0.55, 15 + (i / 4) * 0.55);
                rig.Spawn(UnitKind.Normal, position);
            }
            var airStart = new DVec2(layout == "wall" ? 10 : 14, 16.5);
            long air = rig.Spawn(UnitKind.Air, airStart);
            rig.Movement.SetAirDirection(air, new DVec2(1, 0));
            var catalog = Resources.Load<GameCatalog>("Bootstrap/GameCatalog");
            previewRoot = new GameObject("Crowd verification " + layout);
            var view = previewRoot.AddComponent<WorldView>(); view.Initialize(catalog);
            var presenter = new WorldPresenter(view); presenter.Refresh(rig.Run);
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/Validation/CrowdPreview", rig.Run.Id.ToString("N")));
            Directory.CreateDirectory(output);
            yield return Capture(Path.Combine(output, layout + "-start.png"));
            double maxStep = 0;
            for (int step = 0; step < 600; step++)
            {
                rig.Advance(0.02);
                foreach (var unit in rig.Run.Units)
                {
                    if (unit.Kind == UnitKind.Air) continue;
                    Assert.That(rig.World.Query.IsCircleFree(unit.Position, unit.BodyRadius), Is.True, layout + ": wall penetration");
                    maxStep = Math.Max(maxStep, unit.Position.DistanceTo(rig.Movement.PreviousPositions[unit.Id]));
                    Assert.That(maxStep, Is.LessThan(0.25), layout + ": discontinuous crowd motion");
                }
                if (step % 5 == 0) { presenter.Refresh(rig.Run); yield return null; }
                if (step == 99) yield return Capture(Path.Combine(output, layout + "-air-passage.png"));
            }
            presenter.Refresh(rig.Run);
            Assert.That(rig.Run.Units.Count, Is.EqualTo(14));
            Assert.That(rig.Unit(air).Position.DisplacementTo(WorldPosition.FromLocal(airStart + new DVec2(72, 0))).Length, Is.LessThan(1e-7));
            Assert.That(rig.Run.Units.Where(unit => unit.Kind == UnitKind.Normal).Min(unit => unit.Position.DistanceTo(rig.Player.Position)), Is.LessThan(2));
            Assert.That(Object.FindObjectsByType<UnitView>(FindObjectsSortMode.None).Length, Is.GreaterThanOrEqualTo(13));
            yield return Capture(Path.Combine(output, layout + "-settled.png"));
            File.WriteAllText(Path.Combine(output, "metrics.json"), JsonUtility.ToJson(new PreviewMetrics
            { scenario = layout, runId = rig.Run.Id.ToString("N"), simulatedSeconds = rig.Clock.ElapsedSeconds, maxGroundStep = maxStep, retainedUnits = rig.Run.Units.Count }, true));
            Debug.Log("Crowd preview evidence: " + output);
        }

        private static IEnumerator Capture(string path)
        {
            yield return null;
            ScreenCapture.CaptureScreenshot(path);
            double deadline = Time.realtimeSinceStartupAsDouble + 8;
            while ((!File.Exists(path) || new FileInfo(path).Length == 0) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(File.Exists(path) && new FileInfo(path).Length > 0, Is.True, "Screenshot was not produced: " + path);
        }

        private sealed class PreviewTerrain : IChunkGenerator
        {
            private readonly string layout;
            public PreviewTerrain(string layout) => this.layout = layout;
            public ChunkData Generate(ChunkCoord coord)
            {
                var cells = new bool[1024];
                if (coord.Equals(default(ChunkCoord)))
                    for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                        cells[y * 32 + x] = layout == "narrow" ? y == 15 || y == 17 : layout == "wall" && x == 16;
                return new ChunkData(coord, cells, 1);
            }
        }

        [Serializable]
        private sealed class PreviewMetrics
        {
            public string scenario, runId;
            public double simulatedSeconds, maxGroundStep;
            public int retainedUnits;
        }
    }
}
