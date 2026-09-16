using System;
using System.Collections;
using System.Linq;
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
