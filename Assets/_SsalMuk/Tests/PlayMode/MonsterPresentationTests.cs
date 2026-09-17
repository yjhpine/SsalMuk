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
    public sealed class MonsterPresentationTests
    {
        private GameObject root;
        private RunTestRig rig;
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            rig?.Dispose(); rig = null;
            if (root != null) Object.Destroy(root);
            if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject);
            yield return null; yield return null;
        }

        private WorldView CreateView()
        {
            root = new GameObject("Monster presentation check");
            var view = root.AddComponent<WorldView>();
            view.Initialize(Resources.Load<GameCatalog>("Bootstrap/GameCatalog"));
            rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            return view;
        }

        [UnityTest]
        public IEnumerator MonstersStayUprightWhilePlayerWalksAndHitFeedbackRemains()
        {
            var view = CreateView(); var presenter = new WorldPresenter(view);
            var ids = new[] { rig.Spawn(UnitKind.Normal, new DVec2(4, 0)), rig.Spawn(UnitKind.Air, new DVec2(7, 0)), rig.Spawn(UnitKind.Boss, new DVec2(10, 0)) };
            rig.Movement.SetMoveIntent(rig.Player.Id, new DVec2(1, 0));
            foreach (long id in ids) rig.Movement.SetMoveIntent(id, new DVec2(1, 0));
            rig.Movement.SetAirDirection(ids[1], new DVec2(1, 0));
            presenter.Refresh(rig.Run);
            for (int i = 0; i < 4; i++) { rig.Advance(0.02); presenter.Refresh(rig.Run); }
            foreach (long id in ids)
            {
                var rendered = view.UnitViews.Single(x => x.UnitId == id);
                Assert.That(Quaternion.Angle(rendered.WalkPivot.localRotation, Quaternion.identity), Is.LessThan(0.001f));
                Assert.That(rendered.WalkPivot.localPosition.y, Is.EqualTo(-rig.Unit(id).BodyRadius).Within(1e-5));
            }
            var player = view.UnitViews.Single(x => x.UnitId == rig.Player.Id);
            Assert.That(Quaternion.Angle(player.WalkPivot.localRotation, Quaternion.identity), Is.GreaterThan(1));
            foreach (long id in ids) Assert.That(rig.Hit(id, 1), Is.True);
            presenter.Refresh(rig.Run);
            foreach (long id in ids)
            {
                var rendered = view.UnitViews.Single(x => x.UnitId == id);
                Assert.That(rendered.Body.color, Is.EqualTo(Color.red));
                Assert.That(rendered.HitScaleRoot.localScale.x, Is.GreaterThan(1));
                Assert.That(rig.Unit(id).Knockback.IsActive, Is.True);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActualUnitViewsMoveBetweenFixedSimulationPositions()
        {
            var view = CreateView(); var presenter = new WorldPresenter(view);
            long enemy = rig.Spawn(UnitKind.Normal, new DVec2(4, 0));
            rig.Movement.SetMoveIntent(enemy, new DVec2(-1, 0));
            rig.Advance(0.02);
            presenter.Refresh(rig.Run, 0);
            var rendered = view.UnitViews.Single(x => x.UnitId == enemy);
            Assert.That(rendered.transform.localPosition.x, Is.EqualTo(4).Within(1e-5));
            presenter.Refresh(rig.Run, 0.5);
            Assert.That(rendered.transform.localPosition.x, Is.EqualTo(3.985).Within(1e-5));
            presenter.Refresh(rig.Run, 1);
            Assert.That(rendered.transform.localPosition.x, Is.EqualTo(3.97).Within(1e-5));
            yield return null;
        }
    }
}
