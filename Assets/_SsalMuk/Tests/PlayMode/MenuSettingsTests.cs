using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SsalMuk.Core;
using SsalMuk.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SsalMuk.Tests
{
    public sealed class MenuSettingsTests
    {
        private bool hadPreference;
        private int previousPreference;
        private Keyboard keyboard, previousKeyboard;
        private string evidence;
        [UnitySetUp]
        public IEnumerator Setup()
        {
            hadPreference = PlayerPrefs.HasKey(DisplaySettings.DarkModeKey);
            previousPreference = PlayerPrefs.GetInt(DisplaySettings.DarkModeKey);
            PlayerPrefs.DeleteKey(DisplaySettings.DarkModeKey);
            previousKeyboard = Keyboard.current; keyboard = InputSystem.AddDevice<Keyboard>();
            evidence = Path.Combine(Application.dataPath, "../Logs/Validation/MenuSettings", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(evidence);
            yield return SceneManager.LoadSceneAsync(AppRoot.MainMenuScenePath); yield return null;
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject);
            yield return null; yield return null;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
            if (hadPreference) PlayerPrefs.SetInt(DisplaySettings.DarkModeKey, previousPreference);
            else PlayerPrefs.DeleteKey(DisplaySettings.DarkModeKey);
            PlayerPrefs.Save();
        }
        [UnityTest]
        public IEnumerator MainMenuSettingsAcceptPointerInputAndRememberDarkMode()
        {
            var app = AppRoot.Instance;
            Assert.That(app.Display.DarkMode, Is.True);
            Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            yield return Capture("main-menu.png");
            Click(app.Menu.SettingsButton.gameObject); yield return null;
            Assert.That(app.Menus.Settings.IsVisible, Is.True);
            Assert.That(app.Menus.PauseMenu.IsVisible, Is.False);
            app.Menu.StartButton.onClick.Invoke(); Assert.That(app.Coordinator.Phase, Is.EqualTo(RunPhase.MainMenu), "The modal must block the menu beneath it.");
            Click(app.Menus.Settings.DarkModeToggle.gameObject);
            Assert.That(app.Display.DarkMode, Is.False);
            Assert.That(new DisplaySettings().DarkMode, Is.False, "Reload must restore the saved preference.");
            yield return Capture("settings.png");
            yield return Escape(); Assert.That(app.Menus.Settings.IsVisible, Is.False);
            Click(app.Menu.SettingsButton.gameObject); yield return null;
            Assert.That(app.Menus.Settings.DarkModeToggle.isOn, Is.False);
            Click(app.Menus.Settings.BackButton.gameObject); yield return null;
            Click(app.Menu.StartButton.gameObject); yield return WaitForRunning(app);
            Assert.That(app.Coordinator.Run.IsPaused, Is.False);
            Assert.That(app.Display.DarkMode, Is.False);
        }
        [UnityTest]
        public IEnumerator EscapePausesCombatSettingsReturnToPauseAndResumeRestoresLevelUp()
        {
            var app = AppRoot.Instance; Click(app.Menu.StartButton.gameObject); yield return WaitForRunning(app);
            Assert.That(new ProgressionService(app.Coordinator.Run).AddExperience(5), Is.True);
            double offerDeadline = Time.realtimeSinceStartupAsDouble + 3;
            while (app.Coordinator.Run.CurrentOffer == null && Time.realtimeSinceStartupAsDouble < offerDeadline) yield return null;
            yield return null;
            var run = app.Coordinator.Run; var offer = run.CurrentOffer;
            Assert.That(offer, Is.Not.Null); Assert.That(app.LevelUp.IsVisible, Is.True);
            yield return Escape();
            Assert.That(run.IsPaused, Is.True); Assert.That(run.Clock.IsRunning, Is.False);
            Assert.That(app.Menus.PauseMenu.IsVisible, Is.True); Assert.That(app.LevelUp.IsVisible, Is.False);
            double time = run.Clock.ElapsedSeconds, health = run.Player.Health;
            var position = run.Player.Position;
            var resume = app.Menus.PauseMenu.ResumeButton; var settings = app.Menus.PauseMenu.SettingsButton;
            Canvas.ForceUpdateCanvases();
            Assert.That(resume.transform.position.x, Is.EqualTo(settings.transform.position.x).Within(1));
            Assert.That(resume.transform.position.y, Is.GreaterThan(settings.transform.position.y));
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(run.Clock.ElapsedSeconds, Is.EqualTo(time)); Assert.That(run.Player.Position, Is.EqualTo(position));
            Assert.That(run.Player.Health, Is.EqualTo(health)); Assert.That(run.CurrentOffer, Is.SameAs(offer));
            yield return Capture("pause.png");
            Click(settings.gameObject); Assert.That(app.Menus.Settings.IsVisible, Is.True);
            yield return Escape();
            Assert.That(run.IsPaused, Is.True); Assert.That(app.Menus.PauseMenu.IsVisible, Is.True);
            Assert.That(run.Clock.ElapsedSeconds, Is.EqualTo(time));
            Click(resume.gameObject); yield return new WaitForFixedUpdate(); yield return null;
            Assert.That(run.IsPaused, Is.False); Assert.That(app.Menus.PauseMenu.IsVisible, Is.False);
            Assert.That(app.LevelUp.IsVisible, Is.True); Assert.That(run.CurrentOffer, Is.SameAs(offer));
            Assert.That(run.Clock.ElapsedSeconds, Is.GreaterThan(time));
            yield return Escape(); yield return Escape();
            Assert.That(run.IsPaused, Is.False, "ESC also resumes directly from the pause menu.");
        }
        [UnityTest]
        public IEnumerator DarkModeUpdatesCurrentAndNewChunksWithoutChangingUnitColors()
        {
            var app = AppRoot.Instance; Click(app.Menu.StartButton.gameObject); yield return WaitForRunning(app);
            yield return new WaitForFixedUpdate(); yield return null;
            app.Coordinator.Pause();
            var terrains = Object.FindObjectsByType<TerrainView>(FindObjectsSortMode.None);
            Assert.That(terrains.Length, Is.GreaterThan(0));
            var floor = terrains[0].GetComponent<MeshFilter>().sharedMesh;
            var unit = Object.FindAnyObjectByType<UnitView>().GetComponentsInChildren<SpriteRenderer>().First();
            var unitColor = unit.color;
            app.Display.SetDarkMode(false); var original = floor.colors[0]; yield return Capture("battle-normal.png");
            app.Display.SetDarkMode(true); var dark = floor.colors[0]; yield return Capture("battle-dark.png");
            Assert.That(dark.r, Is.EqualTo(original.r * .12f).Within(.0001f));
            Assert.That(dark.g, Is.EqualTo(original.g * .12f).Within(.0001f));
            Assert.That(unit.color, Is.EqualTo(unitColor));
            var oldIds = terrains.Select(terrain => terrain.GetInstanceID()).ToArray();
            var run = app.Coordinator.Run;
            run.World.MoveUnit(run.Player.Id, run.Player.Position.Offset(new DVec2(128, 128)));
            app.Coordinator.Resume(); yield return new WaitForFixedUpdate(); yield return null;
            var fresh = Object.FindObjectsByType<TerrainView>(FindObjectsSortMode.None).Where(terrain => !oldIds.Contains(terrain.GetInstanceID())).ToArray();
            Assert.That(fresh.Length, Is.GreaterThan(0));
            foreach (var terrain in fresh)
                Assert.That(terrain.GetComponent<MeshFilter>().sharedMesh.colors[0].g, Is.EqualTo(dark.g).Within(.0001f));
        }
        private IEnumerator Escape()
        {
            keyboard.MakeCurrent(); InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        }
        private static void Click(GameObject target)
        {
            Canvas.ForceUpdateCanvases();
            var rect = target.GetComponent<RectTransform>();
            var pointer = new PointerEventData(EventSystem.current)
            { button = PointerEventData.InputButton.Left, position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            Assert.That(hits[0].gameObject.transform.IsChildOf(target.transform), Is.True, "The requested control must be the top pointer target: " + target.name);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        private IEnumerator Capture(string name)
        {
            string path = Path.Combine(evidence, name); yield return null; ScreenCapture.CaptureScreenshot(path);
            double end = Time.realtimeSinceStartupAsDouble + 8;
            while ((!File.Exists(path) || new FileInfo(path).Length == 0) && Time.realtimeSinceStartupAsDouble < end) yield return null;
            Assert.That(File.Exists(path) && new FileInfo(path).Length > 0, Is.True);
        }
        private static IEnumerator WaitForRunning(AppRoot app)
        {
            double end = Time.realtimeSinceStartupAsDouble + 20;
            while (app.Coordinator.Phase != RunPhase.Running && Time.realtimeSinceStartupAsDouble < end)
            { Assert.That(app.Coordinator.LastError, Is.Empty); yield return null; }
            Assert.That(app.Coordinator.Phase, Is.EqualTo(RunPhase.Running)); yield return null;
        }
    }
}
