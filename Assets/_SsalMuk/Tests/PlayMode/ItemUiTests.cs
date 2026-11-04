using System.Collections;
using System.Linq;
using System.IO;
using SsalMuk.Presentation;
using NUnit.Framework;
using SsalMuk.Core;
using SsalMuk.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace SsalMuk.Tests.ItemsUi
{
    public sealed class ItemUiTests
    {
        [UnityTearDown] public IEnumerator Cleanup()
        { if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject); yield return null; yield return null; }
        private IEnumerator Start()
        {
            yield return SceneManager.LoadSceneAsync(AppRoot.MainMenuScenePath); yield return null;
            var task = AppRoot.Instance.Coordinator.StartRunAsync(map: MapKind.Library);
            double deadline = Time.realtimeSinceStartupAsDouble + 20;
            while (!task.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(AppRoot.Instance.Coordinator.Phase, Is.EqualTo(RunPhase.Running));
            Assert.That(AppRoot.Instance.Items, Is.Not.Null, "The run must provide its item inventory and fusion UI.");
        }
        private static IEnumerator Capture(string name, RunModel run)
        {
            yield return new WaitForEndOfFrame();
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/Validation/ItemsUi",run.Id.ToString("N"))); Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, name + ".png"); ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForEndOfFrame(); yield return null; TestContext.WriteLine("Item UI screenshot: " + path);
        }
        [UnityTest]
        public IEnumerator ItemChoiceContinuesCombatAndRapidClicksAwardOnlyOnce()
        {
            yield return Start(); var app = AppRoot.Instance; var run = app.Coordinator.Run;
            run.ItemRewards.AddChest(99999, ItemRarity.Rare); yield return null; yield return new WaitForEndOfFrame();
            var offer = run.ItemRewards.CurrentOffer; double before = run.Clock.ElapsedSeconds;
            Assert.That(app.Items.Rewards.IsVisible, Is.True); Assert.That(app.LevelUp.IsVisible, Is.False);
            yield return Capture("item-reward", run);
            yield return new WaitForSeconds(.3f); Assert.That(run.Clock.ElapsedSeconds, Is.GreaterThan(before + .1));
            app.Items.Rewards.Buttons[0].onClick.Invoke(); app.Items.Rewards.Buttons[1].onClick.Invoke();
            yield return new WaitForSeconds(.08f);
            Assert.That(run.Inventory.Items.Count, Is.EqualTo(1)); Assert.That(run.Inventory.Items[0].Definition.Id, Is.EqualTo(offer.Choices[0].Id));
        }
        [UnityTest]
        public IEnumerator VirtualInventoryScrollSelectsRealInstancesAndAtomicFusionKeepsOrder()
        {
            yield return Start(); var app = AppRoot.Instance; var run = app.Coordinator.Run;
            var runner = Object.FindAnyObjectByType<BattleRunner>(); runner.enabled = false;
            for (int i = 0; i < 67; i++) run.Inventory.TryAdd("c_boots");
            run.Inventory.TryAdd("c_blade"); run.Inventory.TryAdd("c_blade"); run.Inventory.TryAdd("c_feather");
            var ids = run.Inventory.Items.Select(i => i.Id).ToArray(); app.Items.Refresh(run);
            yield return null; Canvas.ForceUpdateCanvases();
            Assert.That(app.Items.Inventory.Slots.Count, Is.LessThan(70));
            CollectionAssert.AreEqual(ids.Take(4), app.Items.Inventory.Slots.Take(4).Select(s => s.ItemId));
            app.Items.Inventory.FusionButton.onClick.Invoke(); app.Items.Inventory.Scroll.verticalNormalizedPosition = 0;
            yield return null; app.Items.Refresh(run);
            foreach (long id in ids.Skip(67)) app.Items.Inventory.Slots.Single(s => s.ItemId == id).Button.onClick.Invoke();
            Assert.That(app.Items.Fusion.FixedButton.interactable, Is.True);
            Assert.That(app.Items.Fusion.ResultText.text, Does.Contain("강철 문장"));
            yield return Capture("fusion-preview", run);
            app.Items.Fusion.FixedButton.onClick.Invoke(); app.Items.Fusion.FixedButton.onClick.Invoke();
            runner.Simulation.Step(.02); app.Items.Refresh(run); yield return null;
            Assert.That(run.Inventory.Items.Count, Is.EqualTo(68));
            CollectionAssert.AreEqual(ids.Take(67), run.Inventory.Items.Take(67).Select(i => i.Id));
            Assert.That(run.Inventory.Items.Last().Definition.Id, Is.EqualTo("u_blade"));
            Assert.That(app.Items.Fusion.SelectedIds.Count, Is.Zero);
            Assert.That(app.Items.Inventory.Slots.Any(s => s.ItemId == run.Inventory.Items.Last().Id), Is.True);
        }
        [UnityTest]
        public IEnumerator RewardTemporarilyHidesFusionAndKeepsValidSelectedMaterials()
        {
            yield return Start(); var app = AppRoot.Instance; var run = app.Coordinator.Run;
            var runner = Object.FindAnyObjectByType<BattleRunner>(); runner.enabled = false;
            for (int i = 0; i < 3; i++) run.Inventory.TryAdd("c_blade");
            app.Items.Refresh(run); app.Items.Inventory.FusionButton.onClick.Invoke();
            app.Items.Inventory.Slots[0].Button.onClick.Invoke(); app.Items.Inventory.Slots[1].Button.onClick.Invoke();
            var selected = app.Items.Fusion.SelectedIds.ToArray();
            run.ItemRewards.AddChest(99999, ItemRarity.Common); app.Items.Refresh(run);
            Assert.That(app.Items.Fusion.IsVisible, Is.False); Assert.That(app.Items.Rewards.IsVisible, Is.True);
            app.Items.Rewards.Buttons[0].onClick.Invoke(); runner.Simulation.Step(.02); app.Items.Refresh(run);
            Assert.That(app.Items.Fusion.IsVisible, Is.True); CollectionAssert.AreEqual(selected, app.Items.Fusion.SelectedIds);
        }
        [UnityTest]
        public IEnumerator HoverNamesUseRarityColorsAndTooltipStaysInsideCanvas()
        {
            yield return Start(); var app = AppRoot.Instance; var run = app.Coordinator.Run;
            var runner = Object.FindAnyObjectByType<BattleRunner>(); runner.enabled = false;
            string[] ids = { "c_blade", "u_blade", "r_fang", "l_fang", "x_fang" };
            Color32[] colors = { new Color32(184,190,199,255), new Color32(112,214,107,255), new Color32(98,176,255,255), new Color32(255,215,90,255), new Color32(255,101,113,255) };
            foreach (string id in ids) run.Inventory.TryAdd(id); app.Items.Refresh(run); yield return null;
            for (int i = 0; i < ids.Length; i++)
            {
                var slot = app.Items.Inventory.Slots[i];
                ExecuteEvents.Execute(slot.Button.gameObject, new PointerEventData(EventSystem.current) { position = new Vector2(Screen.width - 1, 1) }, ExecuteEvents.pointerEnterHandler);
                yield return null; Canvas.ForceUpdateCanvases();
                Assert.That(app.Items.Tooltip.IsVisible, Is.True);
                Assert.That(app.Items.Tooltip.NameText.text, Is.EqualTo(run.Inventory.Catalog.Get(ids[i]).Name));
                Assert.That((Color32)app.Items.Tooltip.NameText.color, Is.EqualTo(colors[i]));
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(app.Items.Tooltip.CanvasRect, app.Items.Tooltip.Rect);
                Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(app.Items.Tooltip.CanvasRect.rect.xMin - .1f));
                Assert.That(bounds.max.y, Is.LessThanOrEqualTo(app.Items.Tooltip.CanvasRect.rect.yMax + .1f));
                Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(app.Items.Tooltip.CanvasRect.rect.yMin - .1f));
                if (i == 2) yield return Capture("item-tooltip", run);
                ExecuteEvents.Execute(slot.Button.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
            }
        }
        [UnityTest]
        public IEnumerator FourWeaponSummaryKeepsEveryUpgradeReachable()
        {
            yield return Start(); var app = AppRoot.Instance; var run = app.Coordinator.Run;
            Object.FindAnyObjectByType<BattleRunner>().enabled = false;
            var growth = new GrowthService(run);
            growth.Equip(WeaponKind.Spear); growth.Equip(WeaponKind.Axe); growth.Equip(WeaponKind.Fireball);
            app.Items.Refresh(run); yield return null; Canvas.ForceUpdateCanvases();
            var levels = app.Items.GetComponentsInChildren<UnityEngine.UI.Text>().Single(t => t.name == "Levels");
            Assert.That(levels.text, Does.Contain("파이어볼").And.Contain("경험치 획득"));
            Assert.That(levels.rectTransform.rect.height, Is.GreaterThanOrEqualTo(levels.preferredHeight),
                "All four weapon and shared upgrade lines must fit the scrollable text content.");
            var scroll = levels.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            Assert.That(scroll, Is.Not.Null);
            scroll.verticalNormalizedPosition = 0; yield return null; Canvas.ForceUpdateCanvases();
            var contentBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, levels.rectTransform);
            Assert.That(contentBounds.min.y, Is.EqualTo(scroll.viewport.rect.yMin).Within(1));
            yield return Capture("four-weapon-upgrades", run);
        }
        [UnityTest]
        public IEnumerator ProcVisualsRenderAndReturnToThePoolWithoutWorldColliders()
        {
            yield return Start(); var app = AppRoot.Instance; var run = app.Coordinator.Run;
            var runner = Object.FindAnyObjectByType<BattleRunner>(); runner.enabled = false;
            run.Inventory.TryAdd("r_lightning"); run.Inventory.TryAdd("r_fire"); run.Inventory.TryAdd("r_shield");
            var origin = run.Player.Position.Offset(new DVec2(.8,0));
            var factory = new GroundEnemyFactory(run.World.Units);
            var small = new UnitDefinition("item-visual-target", UnitKind.Normal, 1, 1, .26, 5, 1);
            var large = new UnitDefinition("item-visual-neighbor", UnitKind.Normal, 100, 1, .26, 5, 1);
            var target = factory.Spawn(new UnitSpawnRequest(run.Id, UnitKind.Normal, origin, small));
            factory.Spawn(new UnitSpawnRequest(run.Id, UnitKind.Normal, origin.Offset(new DVec2(1,1)), large));
            // Hold movement still while the real sword schedule produces a hit and derived effects.
            // This view test must not depend on the low AI choosing to approach the fixture.
            for (int i = 0; i < 150 && runner.Simulation.ItemVisuals.Count == 0; i++)
            {
                double from = run.Clock.ElapsedSeconds; run.Clock.Advance();
                runner.Simulation.Sword.Tick(from, run.Clock.ElapsedSeconds);
                runner.Simulation.ItemEffects.ProcessHits();
            }
            Assert.That(runner.Simulation.ItemVisuals.Count, Is.GreaterThan(0), "The real sword must trigger an item effect before checking its view.");
            var world = Object.FindAnyObjectByType<WorldView>(); new WorldPresenter(world).Refresh(run); app.Items.Refresh(run);
            yield return null; yield return new WaitForEndOfFrame();
            Assert.That(world.GetComponentsInChildren<ItemEffectView>().Length, Is.GreaterThan(0));
            Assert.That(world.GetComponentsInChildren<ItemEffectView>().All(v => v.GetComponentsInChildren<Collider2D>().Length == 0), Is.True);
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/Validation/ItemsUi",run.Id.ToString("N"))); Directory.CreateDirectory(folder);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"item-hud-effects.png")); yield return new WaitForEndOfFrame(); yield return null;
            for (int i = 0; i < 15; i++) run.Clock.Advance();
            runner.Simulation.ItemEffects.Tick(); new WorldPresenter(world).Refresh(run);
            Assert.That(world.GetComponentsInChildren<ItemEffectView>().Length, Is.Zero);
            Assert.That(world.GetComponentsInChildren<ItemEffectView>(true).Length, Is.GreaterThan(0));
            TestContext.WriteLine("Item UI screenshot: " + folder);
        }
    }
}
