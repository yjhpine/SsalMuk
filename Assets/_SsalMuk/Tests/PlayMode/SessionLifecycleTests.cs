using System.Collections;
using System.Linq;
using NUnit.Framework;
using SsalMuk.Core;
using SsalMuk.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SsalMuk.Tests
{
    public sealed class SessionLifecycleTests
    {
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameStartCreatesWorldPlayerThenTwelveEnemiesWithoutPreplacedUnits()
        {
            yield return SceneManager.LoadSceneAsync(AppRoot.MainMenuScenePath);
            yield return null;
            var app = AppRoot.Instance;
            Assert.That(app, Is.Not.Null); Assert.That(app.Coordinator.Phase, Is.EqualTo(RunPhase.MainMenu));
            Assert.That(Object.FindObjectsByType<UnitView>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(app.Menu.StartButton.interactable, Is.True);
            ExecuteEvents.Execute(app.Menu.StartButton.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            app.Menu.StartButton.onClick.Invoke();
            yield return WaitForRunning(app);
            var run = app.Coordinator.Run;
            Assert.That(run.World.Units.Count, Is.EqualTo(13));
            Assert.That(run.Player.Weapons.Kinds, Is.EquivalentTo(new[] { WeaponKind.Sword }));
            Assert.That(run.World.CachedChunkCount, Is.GreaterThanOrEqualTo(9));
            Assert.That(Object.FindObjectsByType<AppRoot>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<WorldView>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<UnitView>(FindObjectsSortMode.None).Length, Is.EqualTo(13));
            double time = run.Clock.ElapsedSeconds; var positions = run.Units.Where(unit => unit.Kind == UnitKind.Normal).ToDictionary(unit => unit.Id, unit => unit.Position);
            yield return new WaitForSeconds(0.6f);
            Assert.That(run.Clock.ElapsedSeconds, Is.GreaterThan(time));
            Assert.That(run.Units.Any(unit => positions.TryGetValue(unit.Id, out var old) && old.DistanceTo(unit.Position) > 0.02), Is.True);
            foreach (var unit in run.Units) Assert.That(run.World.Query.IsCircleFree(unit.Position, unit.BodyRadius), Is.True);
        }

        [UnityTest]
        public IEnumerator DirectBattleUsesSameBootstrapAndKoreanFontHasRealGlyphs()
        {
            yield return SceneManager.LoadSceneAsync(AppRoot.BattleScenePath);
            yield return null;
            var app = AppRoot.Instance; yield return WaitForRunning(app);
            Assert.That(app.Coordinator.Run.World.Units.Count, Is.EqualTo(13));
            var font = Resources.Load<GameCatalog>("Bootstrap/GameCatalog").UiFont;
            Assert.That(font, Is.Not.Null);
            font.RequestCharactersInTexture("쌀먹시작생존시간", 24, FontStyle.Normal);
            foreach (char letter in "쌀먹시작생존시간") Assert.That(font.GetCharacterInfo(letter, out _, 24, FontStyle.Normal), Is.True);
        }

        private static IEnumerator WaitForRunning(AppRoot app)
        {
            Assert.That(app, Is.Not.Null);
            double deadline = Time.realtimeSinceStartupAsDouble + 20;
            while (app.Coordinator.Phase != RunPhase.Running && Time.realtimeSinceStartupAsDouble < deadline)
            {
                Assert.That(app.Coordinator.LastError, Is.Empty);
                yield return null;
            }
            Assert.That(app.Coordinator.Phase, Is.EqualTo(RunPhase.Running));
        }
    }
}
