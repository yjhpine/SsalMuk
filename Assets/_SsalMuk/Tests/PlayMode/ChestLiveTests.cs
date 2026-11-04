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

namespace SsalMuk.Tests.ChestExploration
{
    public sealed class ChestLiveTests
    {
        [UnityTearDown] public IEnumerator Cleanup()
        { if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject); yield return null; yield return null; }
        [UnityTest] public IEnumerator ForestChestShowsGuidanceAndOneManualReward() => Verify(MapKind.Forest);
        [UnityTest] public IEnumerator LibraryChestShowsGuidanceAndOneManualReward() => Verify(MapKind.Library);

        private IEnumerator Verify(MapKind map)
        {
            yield return SceneManager.LoadSceneAsync(AppRoot.MainMenuScenePath); yield return null;
            var app = AppRoot.Instance; var start = app.Coordinator.StartRunAsync(map: map);
            double deadline = Time.realtimeSinceStartupAsDouble + 20;
            while (!start.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(app.Coordinator.Phase, Is.EqualTo(RunPhase.Running));
            var run = app.Coordinator.Run; var runner = Object.FindAnyObjectByType<BattleRunner>(); runner.enabled = false;
            var world = Object.FindAnyObjectByType<WorldView>(); var presenter = new WorldPresenter(world);
            runner.Simulation.Step(.02);
            Assert.That(run.Chests.Target, Is.Not.Null);
            var chest = run.Chests.Target;
            // Frame a real generated chest without changing its record or bypassing collection.
            var anchor = new WorldPosition(chest.Position.Chunk, new DVec2(16.5, 16.5));
            var towardAnchor = chest.Position.DisplacementTo(anchor).Normalized;
            run.World.MoveUnit(run.Player.Id, chest.Position.Offset(towardAnchor * 2));
            presenter.Refresh(run); yield return null;
            Assert.That(world.ChestViews.Any(v => v.ChestId == chest.Id), Is.True);
            Assert.That(world.ChestViews.All(v => v.GetComponentsInChildren<Collider2D>().Length == 0), Is.True);
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/Validation/ChestExploration", run.Id.ToString("N")));
            Directory.CreateDirectory(folder);
            world.ShowChestDirection(new DVec2(60, 15)); yield return null;
            Assert.That(world.ChestGuidance.IsVisible, Is.True);
            Assert.That(world.ChestGuidance.MarkerPosition.x, Is.GreaterThan(0));
            yield return new WaitForEndOfFrame();
            var pixels = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                File.WriteAllBytes(Path.Combine(folder, "chest-guidance.png"), pixels.EncodeToPNG());
                var position = run.Player.Position.DisplacementTo(chest.Position);
                var screenPoint = Camera.main.WorldToScreenPoint(new Vector3((float)position.X, (float)position.Y + .25f));
                var chestView = world.ChestViews.Single(v => v.ChestId == chest.Id);
                var chestRenderer = chestView.GetComponent<MeshRenderer>();
                TestContext.WriteLine($"Chest rendering: image={pixels.width}x{pixels.height}; screen={Screen.width}x{Screen.height}; point={screenPoint}; view={chestView.transform.position}; bounds={chestRenderer.bounds}; visible={chestRenderer.isVisible}; enabled={chestRenderer.enabled}; shader={chestRenderer.sharedMaterial.shader.name}; vertices={chestView.GetComponent<MeshFilter>().sharedMesh.vertexCount}; evidence={folder}");
                Assert.That(GoldPixels(pixels, screenPoint, 45), Is.GreaterThan(15), "The chest must render visible gold/wood pixels, not just have an active view.");
                var arrow = world.ChestGuidance.GetComponentsInChildren<ChestArrowGraphic>().Single();
                var arrowPoint = RectTransformUtility.WorldToScreenPoint(null, arrow.rectTransform.position);
                Assert.That(GoldPixels(pixels, arrowPoint, 14), Is.GreaterThan(15), "The directional triangle must actually render.");
            }
            finally { Object.Destroy(pixels); }
            world.ShowChestDirection(new DVec2(1, 1)); Assert.That(world.ChestGuidance.IsVisible, Is.False);
            world.ShowChestDirection(new DVec2(-60, -50)); Assert.That(world.ChestGuidance.IsVisible, Is.True);
            Assert.That(world.ChestGuidance.MarkerPosition.x, Is.LessThan(0)); Assert.That(world.ChestGuidance.MarkerPosition.y, Is.LessThan(0));
            world.ShowChestDirection(null); Assert.That(world.ChestGuidance.IsVisible, Is.False);
            var level = run.Player.Level; double before = run.Clock.ElapsedSeconds;
            run.World.MoveUnit(run.Player.Id, chest.Position); runner.Simulation.Step(.02); presenter.Refresh(run);
            // AppRoot presents reward UI in LateUpdate; observe it after that phase.
            yield return null; yield return new WaitForEndOfFrame();
            Assert.That(chest.Opened, Is.True); Assert.That(run.Clock.ElapsedSeconds, Is.GreaterThan(before));
            Assert.That(run.Player.Level, Is.EqualTo(level)); Assert.That(run.HasItemChoice, Is.True);
            Assert.That(app.Items.Rewards.IsVisible, Is.True); Assert.That(app.Items.Rewards.Heading.text, Is.EqualTo("상자 보상"));
            Assert.That(app.Items.Rewards.Buttons.Count, Is.EqualTo(3));
            Assert.That(world.ChestViews.Any(v => v.ChestId == chest.Id), Is.False);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "chest-reward.png")); yield return new WaitForEndOfFrame(); yield return null;
            app.Coordinator.Pause(); runner.Simulation.Step(.02); yield return null; yield return new WaitForEndOfFrame(); Assert.That(app.Items.Rewards.IsVisible, Is.False);
            app.Coordinator.Resume(); yield return null; yield return new WaitForEndOfFrame();
            var pending = run.ItemRewards.PendingCount; long offerId = run.ItemRewards.CurrentOffer.Id;
            app.Items.Rewards.Buttons[0].onClick.Invoke(); app.Items.Rewards.Buttons[1].onClick.Invoke(); runner.Simulation.Step(.02);
            Assert.That(run.ItemRewards.PendingCount, Is.EqualTo(pending - 1));
            Assert.That(run.ItemRewards.TryQueueChoice(run.Id, offerId, 0), Is.False);
            Assert.That(run.Chests.TryOpen(chest.Id), Is.False);
            TestContext.WriteLine("Chest live evidence: " + folder);
        }
        private static int GoldPixels(Texture2D texture, Vector2 center, int radius)
        {
            int count = 0;
            for (int y = Mathf.Max(0, (int)center.y - radius); y < Mathf.Min(texture.height, (int)center.y + radius); y++)
                for (int x = Mathf.Max(0, (int)center.x - radius); x < Mathf.Min(texture.width, (int)center.x + radius); x++)
                { var c = texture.GetPixel(x, y); if (c.r > .45f && c.g > .25f && c.r > c.g * 1.05f && c.g > c.b * 1.3f) count++; }
            return count;
        }
    }
}
