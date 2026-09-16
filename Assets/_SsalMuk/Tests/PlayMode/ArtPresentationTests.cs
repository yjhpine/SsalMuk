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
namespace SsalMuk.Tests
{
    public sealed class ArtPresentationTests
    {
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (AppRoot.Instance != null) UnityEngine.Object.Destroy(AppRoot.Instance.gameObject);
            yield return null; yield return null;
        }
        [UnityTest]
        public IEnumerator ImportedArtAndExperienceContactAreCapturedFromActualBattleModels()
        {
            yield return SceneManager.LoadSceneAsync(AppRoot.MainMenuScenePath); yield return null;
            var app = AppRoot.Instance; app.Menu.StartButton.onClick.Invoke();
            double deadline = Time.realtimeSinceStartupAsDouble + 20;
            while (app.Coordinator.Phase != RunPhase.Running && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(app.Coordinator.Phase, Is.EqualTo(RunPhase.Running));
            var run = app.Coordinator.Run; var runner = UnityEngine.Object.FindAnyObjectByType<BattleRunner>();
            // This screenshot fixture advances the same simulation in exact ticks, then holds the image for capture.
            runner.enabled = false; var simulation = runner.Simulation;
            var view = UnityEngine.Object.FindAnyObjectByType<WorldView>(); var presenter = new WorldPresenter(view);
            var growth = new GrowthService(run); foreach (var kind in new[] { WeaponKind.Spear, WeaponKind.Axe, WeaponKind.Fireball }) growth.Equip(kind);
            var origin = run.Player.Position;
            var normal = new UnitDefinition("art-normal", UnitKind.Normal, 10000, 1.5, 0.26, 5, 1);
            new GroundEnemyFactory(run.World.Units).Spawn(new UnitSpawnRequest(run.Id, UnitKind.Normal, origin.Offset(new DVec2(1.2, 0.7)), normal));
            var air = run.Definitions.GetUnit(UnitKind.Air);
            new AirEnemyFactory(run.World.Units).Spawn(new UnitSpawnRequest(run.Id, UnitKind.Air, origin.Offset(new DVec2(-3, 2)), air, new DVec2(1, 0)));
            var boss = run.Definitions.GetUnit(UnitKind.Boss); WorldPosition bossAt = origin.Offset(new DVec2(3, 2));
            for (int i = 0; i < 128 && !run.World.Query.IsCircleFree(bossAt, boss.BodyRadius); i++)
            { double angle = i * Math.PI / 8; bossAt = origin.Offset(new DVec2(Math.Cos(angle), Math.Sin(angle)) * (3 + i / 16 * 0.5)); }
            Assert.That(run.World.Query.IsCircleFree(bossAt, boss.BodyRadius), Is.True);
            new GroundEnemyFactory(run.World.Units).Spawn(new UnitSpawnRequest(run.Id, UnitKind.Boss, bossAt, boss));
            long green = run.World.AddExperience(origin.Offset(new DVec2(-1.2, 0)), 1, run.Clock.ElapsedSeconds);
            run.World.AddExperience(origin.Offset(new DVec2(-3, -1)), 5, run.Clock.ElapsedSeconds);
            run.World.AddExperience(origin.Offset(new DVec2(3, -1)), 25, run.Clock.ElapsedSeconds);
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/Validation/Art", run.Id.ToString("N")));
            Directory.CreateDirectory(folder);
            simulation.Step(0.02); presenter.Refresh(run);
            Assert.That(run.World.TryGetExperience(green, out var flying), Is.True); Assert.That(flying.State, Is.EqualTo(ExperienceState.Attracting));
            Assert.That(run.Player.Growth.ExperienceIntoLevel, Is.EqualTo(System.Numerics.BigInteger.Zero));
            yield return Capture(Path.Combine(folder, "experience-attraction.png"));
            int steps = 0;
            while (run.World.TryGetExperience(green, out _) && steps++ < 100) simulation.Step(0.02);
            Assert.That(run.World.TryGetExperience(green, out _), Is.False); presenter.Refresh(run);
            Assert.That(run.Player.Growth.ExperienceIntoLevel, Is.GreaterThan(System.Numerics.BigInteger.Zero));
            yield return Capture(Path.Combine(folder, "experience-contact.png"));
            Assert.That(view.UnitViews.Any(x => x.Body.sprite.name == "Knight"), Is.True);
            Assert.That(view.UnitViews.Any(x => x.Body.sprite.name == "Ogre"), Is.True);
            growth.Upgrade(WeaponKind.Sword, UpgradeKind.Range, 10);
            for (int i = 0; i < 150 && !simulation.Sword.ActiveAttacks.Any(x => x.Stats.Range > 2); i++) simulation.Step(0.02);
            Assert.That(simulation.Sword.ActiveAttacks.Any(x => x.Stats.Range > 2), Is.True);
            presenter.Refresh(run); yield return Capture(Path.Combine(folder, "upgraded-range.png"));
            var contactDefinition = new UnitDefinition("art-hit", UnitKind.Air, 10000, 6, 0.26, 5, 1);
            new AirEnemyFactory(run.World.Units).Spawn(new UnitSpawnRequest(run.Id, UnitKind.Air, run.Player.Position, contactDefinition));
            simulation.Step(0.02); presenter.Refresh(run);
            var playerView = view.UnitViews.Single(x => x.UnitId == run.Player.Id);
            Assert.That(playerView.Body.color, Is.EqualTo(Color.red)); Assert.That(playerView.HitScaleRoot.localScale.x, Is.GreaterThan(1));
            yield return Capture(Path.Combine(folder, "player-hit.png"));
            foreach (var label in app.GetComponentsInChildren<UnityEngine.UI.Text>().Where(x => x.name == "Time" || x.name == "Remaining" || x.name == "Kills"))
                Assert.That(FontUvMismatches(label), Is.Empty, "Rendered text must use the current font atlas: " + label.name);
            File.WriteAllText(Path.Combine(folder, "observations.json"), JsonUtility.ToJson(new ArtEvidence {
                runId = run.Id.ToString(), width = Screen.width, height = Screen.height, seed = run.Seed,
                elapsed = run.Clock.ElapsedSeconds, health = run.Player.Health, level = run.Player.Level.ToString(),
                source = "Actual GameStart and RunSimulation; exact-step screenshot fixture", sprites = view.UnitViews.Select(x => x.Body.sprite.name).Distinct().ToArray(),
                weapons = run.Player.Weapons.Kinds.Select(x => x.ToString()).ToArray(), visibleExperience = view.VisibleExperienceCount,
                ui = app.GetComponentsInChildren<UnityEngine.UI.Text>().Select(label => new UiEvidence { name = label.name, text = label.text,
                    visibleCharacters = label.cachedTextGenerator.characterCountVisible, vertices = label.cachedTextGenerator.vertexCount,
                    rect = label.rectTransform.rect.ToString(), fontSize = label.fontSize }).ToArray() }, true));
            Debug.Log("Art evidence: " + folder);
        }
        private static IEnumerator Capture(string path)
        {
            yield return null; yield return null; yield return new WaitForEndOfFrame();
            var screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                File.WriteAllBytes(path, screenshot.EncodeToPNG());
                if (path.EndsWith("player-hit.png", StringComparison.Ordinal))
                {
                    var timer = AppRoot.Instance.GetComponentsInChildren<UnityEngine.UI.Text>().Single(x => x.name == "Time");
                    var vertices = timer.canvasRenderer.GetMesh().vertices;
                    for (int glyph = 0; glyph < timer.text.Length; glyph++)
                    {
                        var points = vertices.Skip(glyph * 4).Take(4).Select(v => RectTransformUtility.WorldToScreenPoint(null, timer.transform.TransformPoint(v))).ToArray();
                        int left = Mathf.Clamp(Mathf.FloorToInt(points.Min(p => p.x)), 0, screenshot.width - 1);
                        int right = Mathf.Clamp(Mathf.CeilToInt(points.Max(p => p.x)), 0, screenshot.width - 1);
                        int bottom = Mathf.Clamp(Mathf.FloorToInt(points.Min(p => p.y)), 0, screenshot.height - 1);
                        int top = Mathf.Clamp(Mathf.CeilToInt(points.Max(p => p.y)), 0, screenshot.height - 1);
                        int lit = 0;
                        for (int y = bottom; y <= top; y++) for (int x = left; x <= right; x++)
                        { var pixel = screenshot.GetPixel(x, y); if (pixel.r > 0.7f && pixel.g > 0.7f && pixel.b > 0.7f) lit++; }
                        Assert.That(lit, Is.GreaterThan(3), "Timer glyph has no rendered pixels: " + timer.text[glyph] + " at " + glyph);
                    }
                }
            }
            finally { UnityEngine.Object.Destroy(screenshot); }
            Assert.That(File.Exists(path) && new FileInfo(path).Length > 0, Is.True);
        }
        private static string FontUvMismatches(UnityEngine.UI.Text label)
        {
            var mesh = UnityEngine.Object.Instantiate(label.canvasRenderer.GetMesh());
            try
            {
                var uvs = mesh.uv; int glyph = 0; string mismatches = "";
                int size = (int)(label.fontSize * label.pixelsPerUnit);
                foreach (char character in label.text)
                {
                    if (char.IsWhiteSpace(character)) continue;
                    if (!label.font.GetCharacterInfo(character, out var info, size, label.fontStyle)) return "Missing glyph at size " + size;
                    var expected = new[] { info.uvTopLeft, info.uvTopRight, info.uvBottomLeft, info.uvBottomRight };
                    if (glyph * 4 + 3 >= uvs.Length) return "Missing rendered quad";
                    for (int corner = 0; corner < 4; corner++)
                        if (!expected.Any(uv => Vector2.Distance(uv, uvs[glyph * 4 + corner]) < 1e-5)) { mismatches += character; break; }
                    glyph++;
                }
                return mismatches;
            }
            finally { UnityEngine.Object.Destroy(mesh); }
        }
        [Serializable]
        private sealed class ArtEvidence
        {
            public string runId, source, level;
            public int width, height, seed, visibleExperience;
            public double elapsed, health;
            public string[] sprites, weapons;
            public UiEvidence[] ui;
        }
        [Serializable]
        private sealed class UiEvidence { public string name, text, rect; public int visibleCharacters, vertices, fontSize; }
    }
}
