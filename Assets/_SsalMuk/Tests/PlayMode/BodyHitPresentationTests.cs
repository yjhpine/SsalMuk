using System;
using System.Collections;
using NUnit.Framework;
using SsalMuk.Core;
using SsalMuk.Unity;
using UnityEngine;
using UnityEngine.TestTools;

namespace SsalMuk.Tests
{
    public sealed class BodyHitPresentationTests
    {
        [UnityTest]
        public IEnumerator ActualCatalogUsesSeparateRadiiAndPreservesFeetDuringHitFeedback()
        {
            var catalog = Resources.Load<GameCatalog>("Bootstrap/GameCatalog");
            var root = UnityEngine.Object.Instantiate(catalog.UnitViewPrefab);
            try
            {
                using var rig = RunTestRig.Create(enableAi: false, definitions: catalog.Defaults.CreateCatalog());
                var view = root.GetComponent<UnitView>();
                foreach (UnitKind kind in Enum.GetValues(typeof(UnitKind)))
                {
                    var definition = rig.Run.Definitions.GetUnit(kind); var art = catalog.Visuals.Unit(kind);
                    double diameter = Math.Min(art.Height, art.Height * art.Sprite.bounds.size.x / art.Sprite.bounds.size.y);
                    Assert.That(definition.BodyRadius * 2, Is.EqualTo(diameter * (kind == UnitKind.Player ? .8 : 1)).Within(1e-6));
                    Assert.That(definition.HurtRadius * 2, Is.EqualTo(diameter * 1.1).Within(1e-6));
                    var unit = kind == UnitKind.Player ? rig.Player : rig.Unit(rig.Spawn(kind, new DVec2(4, 0), 100));
                    view.ResetVisuals(); view.Show(unit, DVec2.Zero, catalog);
                    float foot = view.WalkPivot.localPosition.y;
                    Assert.That(foot, Is.EqualTo(-definition.VisualFootOffset).Within(1e-6));
                    Assert.That(rig.Hit(unit.Id, 1), Is.True); view.Show(unit, DVec2.Zero, catalog);
                    Assert.That(view.HitScaleRoot.localScale.x, Is.GreaterThan(1));
                    Assert.That(view.WalkPivot.localPosition.y, Is.EqualTo(foot));
                    Assert.That(unit.HurtRadius, Is.EqualTo(definition.HurtRadius));
                    Assert.That(root.transform.Find("GroundShadow"), Is.Null);
                }
                yield return null;
            }
            finally { UnityEngine.Object.Destroy(root); }
        }
    }
}
