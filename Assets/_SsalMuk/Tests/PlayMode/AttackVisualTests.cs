using System;
using System.Collections;
using System.Linq;
using System.IO;
using NUnit.Framework;
using SsalMuk.Core;
using SsalMuk.Presentation;
using SsalMuk.Unity;
using UnityEngine;
using UnityEngine.TestTools;
namespace SsalMuk.Tests
{
    public sealed class AttackVisualTests
    {
        [TestCase(WeaponKind.Sword)] [TestCase(WeaponKind.Spear)] [TestCase(WeaponKind.Axe)]
        public void MeleeHandleAndContactStayAlignedToTheSnapshotAtEveryRange(WeaponKind kind)
        {
            var root = new GameObject("MeleeAlignmentFixture"); var catalog = Resources.Load<GameCatalog>("Bootstrap/GameCatalog");
            try
            {
                var view = root.AddComponent<AttackView>(); view.Initialize(catalog);
                foreach (double multiplier in new[] { 1.0, 1.8, 10.0, 18.0 })
                foreach (double progress in new[] { .1, .5, .9 })
                {
                    using var rig = RunTestRig.Create(enableAi: false);
                    if (kind != WeaponKind.Sword) rig.Equip(kind);
                    rig.Spawn(UnitKind.Normal, new DVec2(60, 80), 1000);
                    var definition = rig.Run.Definitions.GetWeapon(kind);
                    using var runtime = new WeaponRuntime(rig.Run, kind, rig.Movement, rig.Damage,
                        () => new WeaponStats(8, definition.Range * multiplier, 1));
                    runtime.Tick(0, definition.ActiveSeconds * progress);
                    var snapshot = new AttackShapeSnapshot(runtime.ActiveAttacks.Single(), definition);
                    view.Show(snapshot, new DVec2(3, -2));
                    var art = catalog.Visuals.Weapon(kind); var renderer = view.WeaponSprite;
                    var handle = renderer.transform.TransformPoint(new Vector3(renderer.sprite.bounds.min.x, 0, 0));
                    var contact = renderer.transform.TransformPoint(new Vector3(
                        renderer.sprite.bounds.min.x + renderer.sprite.bounds.size.x * art.ContactFraction, 0, 0));
                    var expectedTip = root.transform.position + new Vector3((float)snapshot.Tip.X, (float)snapshot.Tip.Y, 0);
                    Assert.That(Vector3.Distance(handle, root.transform.position), Is.LessThan(1e-4), kind + " handle detached from the attack origin.");
                    Assert.That(Vector3.Distance(contact, expectedTip), Is.LessThan(1e-4), kind + " contact does not match the damage snapshot.");
                    var effect = root.transform.Find("Effect");
                    Assert.That(Vector3.Distance(effect.position, expectedTip), Is.LessThan(1e-4), kind + " effect detached from the weapon tip.");
                    Assert.That(Vector3.Distance(effect.GetComponentInChildren<SpriteRenderer>().bounds.center, expectedTip), Is.LessThan(1e-4),
                        kind + " effect source pivot shifted its visible center away from the tip.");
                    Assert.That(view.Snapshot, Is.EqualTo(snapshot));
                    if (progress == .5 && multiplier != 18) CaptureFrame(root, kind, multiplier);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static void CaptureFrame(GameObject root, WeaponKind kind, double multiplier)
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/Validation/WeaponTips"));
            Directory.CreateDirectory(directory);
            var cameraObject = new GameObject("WeaponEvidenceCamera");
            var target = new RenderTexture(960, 540, 24);
            var pixels = new Texture2D(960, 540, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>()) child.gameObject.layer = 30;
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
                camera.orthographic = true; camera.cullingMask = 1 << 30; camera.aspect = 960f / 540;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.06f, .07f, .09f);
                var bounds = new Bounds(root.transform.position, Vector3.zero);
                foreach (var renderer in root.GetComponentsInChildren<Renderer>()) if (renderer.enabled) bounds.Encapsulate(renderer.bounds);
                camera.orthographicSize = Mathf.Max(bounds.extents.y + .35f, bounds.extents.x / camera.aspect + .35f);
                camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10);
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(directory, kind + "-" + multiplier.ToString(System.Globalization.CultureInfo.InvariantCulture) + "x.png"), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
        [UnityTest]
        public IEnumerator FourActualAttacksUseImportedWeaponsAndTheSameUpgradedRangeSnapshots()
        {
            var root = new GameObject("AttackViewFixture"); var catalog = Resources.Load<GameCatalog>("Bootstrap/GameCatalog");
            try
            {
                using var rig = RunTestRig.Create(enableAi: false); var worldView = root.AddComponent<WorldView>(); worldView.Initialize(catalog);
                var presenter = new WorldPresenter(worldView);
                foreach (WeaponKind kind in Enum.GetValues(typeof(WeaponKind))) { if (kind != WeaponKind.Sword) rig.Equip(kind); rig.Upgrade(kind, UpgradeKind.Range, 10); }
                rig.Spawn(UnitKind.Normal, new DVec2(8, 0), 1000); rig.Advance(0.06); presenter.Refresh(rig.Run);
                Assert.That(worldView.AttackViews.Count(), Is.EqualTo(4));
                foreach (var view in worldView.AttackViews)
                {
                    var shape = view.Snapshot; double expected = StatCalculator.Calculate(rig.Run.Definitions.GetWeapon(shape.Kind), rig.Player.Weapons.Get(shape.Kind), rig.Run.GrowthSettings).Range;
                    Assert.That(view.DisplayedRange, Is.EqualTo(expected)); Assert.That(view.WeaponSprite.sprite, Is.SameAs(catalog.Visuals.Weapon(shape.Kind).Sprite));
                    Assert.That(view.WeaponSprite.sprite.name, Is.Not.EqualTo("DevelopmentBody"));
                    Assert.That(shape.Key.RunId, Is.EqualTo(rig.Run.Id));
                    if (shape.Kind == WeaponKind.Sword || shape.Kind == WeaponKind.Axe)
                    {
                        Assert.That(shape.Tip.Length, Is.EqualTo(expected).Within(1e-7));
                        var mesh = view.transform.Find("AttackRange").GetComponent<MeshFilter>().sharedMesh;
                        double outer = expected + (shape.Kind == WeaponKind.Axe ? shape.Width : 0);
                        Assert.That(mesh.vertices.Max(x => x.magnitude), Is.EqualTo(outer).Within(1e-5), "The rendered mesh must grow with its real attack range.");
                    }
                    if (shape.Kind == WeaponKind.Spear) Assert.That(view.ShapeBounds.max.x, Is.EqualTo(expected * shape.Progress + shape.Width / 2).Within(1e-5));
                }
                Assert.That(root.GetComponentsInChildren<ProjectileView>().Length, Is.GreaterThan(0));
                Assert.That(worldView.UnitViews.Single(x => x.UnitId == rig.Player.Id).WalkPivot.localRotation, Is.EqualTo(Quaternion.identity));
                yield return null;
            }
            finally { UnityEngine.Object.Destroy(root); }
        }
    }
}
