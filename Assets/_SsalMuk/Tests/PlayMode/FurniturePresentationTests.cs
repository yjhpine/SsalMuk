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
    public sealed class FurniturePresentationTests
    {
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject);
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator GameStartCreatesFurnitureWithStaticFootprintsAndStableOriginSorting()
        {
            yield return SceneManager.LoadSceneAsync(AppRoot.MainMenuScenePath); yield return null;
            Assert.That(Object.FindObjectsByType<TerrainView>(FindObjectsSortMode.None), Is.Empty);
            var app = AppRoot.Instance; app.Menu.StartButton.onClick.Invoke();
            double deadline = Time.realtimeSinceStartupAsDouble + 20;
            while (app.Coordinator.Phase != RunPhase.Running && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(app.Coordinator.Phase, Is.EqualTo(RunPhase.Running));
            var runner = Object.FindAnyObjectByType<BattleRunner>(); runner.enabled = false;
            var run = app.Coordinator.Run;
            var chunk = run.World.CachedTerrain.FirstOrDefault(x => x.Furniture.Count > 0);
            Assert.That(chunk, Is.Not.Null);
            var item = chunk.Furniture[0];
            var footprint = new WorldPosition(chunk.Coord, new DVec2(item.X + .5, item.Y + .5));
            Assert.That(run.World.Query.IsCircleFree(footprint, run.Player.BodyRadius), Is.False);
            var playerAt = footprint.Offset(new DVec2(0, -2));
            Assert.That(run.World.Query.IsCircleFree(playerAt, run.Player.BodyRadius), Is.True);
            run.World.MoveUnit(run.Player.Id, playerAt);
            // The normal runner prepares neighbouring terrain in LateUpdate. This fixed capture
            // disables that runner, so prepare the same 3x3 neighbourhood after moving the fixture.
            for (long y = -1; y <= 1; y++) for (long x = -1; x <= 1; x++)
                run.World.GetChunk(new ChunkCoord(playerAt.Chunk.X + x, playerAt.Chunk.Y + y));
            var view = Object.FindAnyObjectByType<WorldView>(); var presenter = new WorldPresenter(view); presenter.Refresh(run);
            var terrain = Object.FindObjectsByType<TerrainView>(FindObjectsSortMode.None).First(x => x.Fingerprint == chunk.Fingerprint);
            var sprites = terrain.GetComponentsInChildren<SpriteRenderer>();
            Assert.That(sprites.Length, Is.EqualTo(chunk.Furniture.Count));
            Assert.That(terrain.GetComponentsInChildren<Collider2D>(), Is.Empty);
            Assert.That(terrain.GetComponentsInChildren<Rigidbody2D>(), Is.Empty);
            foreach (var sprite in sprites)
            {
                Assert.That(sprite.sprite.pivot.y, Is.Zero);
                Assert.That(sprite.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(sprite.sprite.texture.mipmapCount, Is.EqualTo(1));
                Assert.That(sprite.transform.localScale.x, Is.GreaterThan(0));
            }
            int priorOrder = sprites[0].sortingOrder;
            var oldOrigin = terrain.transform.localPosition;
            terrain.SetRelativeOrigin(new DVec2(oldOrigin.x, oldOrigin.y + 32));
            Assert.That(sprites[0].sortingOrder, Is.EqualTo(priorOrder - 960));
            terrain.SetRelativeOrigin(new DVec2(oldOrigin.x, oldOrigin.y));
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/Validation/Furniture", run.Id.ToString("N")));
            Directory.CreateDirectory(folder);
            yield return null; yield return null; yield return new WaitForEndOfFrame();
            var capture = ScreenCapture.CaptureScreenshotAsTexture();
            try { File.WriteAllBytes(Path.Combine(folder, "game-start-furniture.png"), capture.EncodeToPNG()); }
            finally { Object.Destroy(capture); }
            Debug.Log("Furniture evidence: " + folder);
        }
    }
}
