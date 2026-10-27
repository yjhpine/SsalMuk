using System.Collections;
using NUnit.Framework;
using SsalMuk.Core;
using SsalMuk.Unity;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using System.Linq;

namespace SsalMuk.Tests.MapFeature
{
    public sealed class PlayerAnimationTests
    {
        [UnityTest]
        public IEnumerator PlayerUsesClipsWhilePauseAndPoolReuseKeepPoseStable()
        {
            var catalog = Resources.Load<GameCatalog>("Bootstrap/GameCatalog");
            var go = Object.Instantiate(catalog.UnitViewPrefab);
            try
            {
                using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
                var view = go.GetComponent<UnitView>();
                rig.Movement.SetMoveIntent(rig.Player.Id, new DVec2(1, 0)); rig.Movement.Step(.02);
                view.Show(rig.Player, DVec2.Zero, catalog, 0, .1);
                Assert.That(go.GetComponentInChildren<Animator>(), Is.Not.Null, "Player locomotion must use the lesson's animation clips/controller.");
                Assert.That(Quaternion.Angle(view.WalkPivot.localRotation, Quaternion.identity), Is.GreaterThan(1));
                var angle = view.WalkPivot.localRotation; var position = view.WalkPivot.localPosition;
                yield return null; yield return null;
                view.Show(rig.Player, DVec2.Zero, catalog, 0, 0);
                Assert.That(view.WalkPivot.localRotation, Is.EqualTo(angle));
                Assert.That(view.WalkPivot.localPosition, Is.EqualTo(position));
                rig.Movement.SetMoveIntent(rig.Player.Id, DVec2.Zero); rig.Movement.Step(.02);
                view.Show(rig.Player, DVec2.Zero, catalog, .4, .4);
                Assert.That(Quaternion.Angle(view.WalkPivot.localRotation, Quaternion.identity), Is.LessThan(.01));
                Assert.That(view.WalkPivot.localPosition.y, Is.EqualTo(-.28f).Within(.0001f));
                view.ResetVisuals();
                var enemy = rig.Unit(rig.Spawn(UnitKind.Normal, new DVec2(2, 0)));
                rig.Movement.SetMoveIntent(enemy.Id, new DVec2(1, 0)); rig.Movement.Step(.02);
                view.Show(enemy, new DVec2(2, 0), catalog, .5, .1);
                Assert.That(view.WalkPivot.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(rig.Player.BodyRadius, Is.EqualTo(.28));
            }
            finally { Object.Destroy(go); }
        }
        [UnityTest]
        public IEnumerator MenuOffersBothMapsBeforeStarting()
        {
            var root = new GameObject("MenuMapFixture");
            try
            {
                var menu = root.AddComponent<MainMenuView>();
                menu.Initialize(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
                Assert.That(root.GetComponentsInChildren<UnityEngine.UI.Button>(),
                    Has.Some.Property("name").EqualTo("Map_Forest"));
                Assert.That(root.GetComponentsInChildren<UnityEngine.UI.Button>(),
                    Has.Some.Property("name").EqualTo("Map_Library"));
                yield return null;
            }
            finally { Object.Destroy(root); }
        }
        [UnityTest]
        public IEnumerator SelectedMapReachesWorldAndSurvivesRestartWithBatchedGrass()
        {
            yield return SceneManager.LoadSceneAsync(AppRoot.MainMenuScenePath); yield return null;
            var app = AppRoot.Instance;
            try
            {
                foreach (var map in new[] { MapKind.Library, MapKind.Forest })
                {
                    var button = app.Menu.GetComponentsInChildren<UnityEngine.UI.Button>().Single(x => x.name == "Map_" + map);
                    button.onClick.Invoke(); Assert.That(app.Menu.SelectedMap, Is.EqualTo(map));
                    app.Menu.StartButton.onClick.Invoke(); yield return Running();
                    Assert.That(app.Coordinator.Run.World.Map, Is.EqualTo(map));
                    Assert.That(app.Coordinator.Run.Clock.ElapsedTicks, Is.GreaterThan(0), "The game must actually advance.");
                    app.Coordinator.Pause();
                    var terrain = Object.FindObjectsByType<TerrainView>(FindObjectsSortMode.None);
                    Assert.That(terrain.Length, Is.GreaterThan(0));
                    if (map == MapKind.Forest)
                    {
                        var grass = terrain.Select(x => x.transform.Find("GrassBatch")).Where(x => x != null).ToArray();
                        Assert.That(grass.Length, Is.GreaterThan(0));
                        foreach (var batch in grass)
                        {
                            Assert.That(batch.childCount, Is.Zero);
                            Assert.That(batch.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(400));
                            Assert.That(batch.GetComponent<Collider2D>(), Is.Null);
                        }
                    }
                    app.Coordinator.Resume();
                    var player = app.Coordinator.Run.Player;
                    using (var movement = new MovementSystem(app.Coordinator.Run.World, new NavigationService(app.Coordinator.Run.World), player))
                    using (var damage = new DamageService(app.Coordinator.Run, movement))
                    {
                        damage.TryApply(new DamageRequest(new HitKey(app.Coordinator.Run.Id, app.Coordinator.Run.AllocateAttackId(), 0, 0),
                            player.Id, player.Id, 100000, DVec2.Zero), app.Coordinator.Run.Clock.ElapsedSeconds);
                    }
                    double deadline = Time.realtimeSinceStartupAsDouble + 4;
                    while (app.Coordinator.Phase != RunPhase.Results && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                    Assert.That(app.Coordinator.Phase, Is.EqualTo(RunPhase.Results));
                    var restart = app.Coordinator.RestartAsync(); while (!restart.IsCompleted) yield return null;
                    yield return Running(); Assert.That(app.Coordinator.Run.World.Map, Is.EqualTo(map));
                    var menu = app.Coordinator.ReturnToMenuAsync(); while (!menu.IsCompleted) yield return null;
                    yield return null;
                }
            }
            finally { if (app != null) Object.Destroy(app.gameObject); }
            IEnumerator Running()
            {
                double end = Time.realtimeSinceStartupAsDouble + 12;
                while (app.Coordinator.Phase != RunPhase.Running && Time.realtimeSinceStartupAsDouble < end)
                { Assert.That(app.Coordinator.LastError, Is.Empty); yield return null; }
                Assert.That(app.Coordinator.Phase, Is.EqualTo(RunPhase.Running));
                yield return new WaitForFixedUpdate(); yield return null;
            }
        }
    }
}
