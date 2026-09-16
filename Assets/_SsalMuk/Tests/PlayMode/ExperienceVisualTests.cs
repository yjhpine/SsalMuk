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
    public sealed class ExperienceVisualTests
    {
        [UnityTest]
        public IEnumerator OrbViewsShowThreeValuesAndFollowTheModelUntilBodyContactPaysOnce()
        {
            var catalog = Resources.Load<GameCatalog>("Bootstrap/GameCatalog"); var go = new GameObject("ExperienceFixture");
            try
            {
                using var rig = RunTestRig.Create(enableAi: false); var view = go.AddComponent<WorldView>(); view.Initialize(catalog);
                var presenter = new WorldPresenter(view);
                long green = rig.DropXp(new DVec2(1.2, 0), 1), blue = rig.DropXp(new DVec2(3, 1), 5), red = rig.DropXp(new DVec2(3, -1), 25);
                presenter.Refresh(rig.Run);
                Assert.That(view.ExperienceViews.Select(x => x.Tier), Is.EquivalentTo(new[] { ExperienceTier.Green, ExperienceTier.Blue, ExperienceTier.Red }));
                Assert.That(view.ExperienceViews.Select(x => x.DisplayColor).Distinct().Count(), Is.EqualTo(3));
                Assert.That(rig.Player.Growth.ExperienceIntoLevel, Is.EqualTo(System.Numerics.BigInteger.Zero));
                rig.Advance(0.02); presenter.Refresh(rig.Run);
                Assert.That(view.ExperienceViews.Single(x => x.ExperienceId == green).DisplayedState, Is.EqualTo(ExperienceState.Attracting));
                Assert.That(rig.Player.Growth.ExperienceIntoLevel, Is.EqualTo(System.Numerics.BigInteger.Zero), "Attraction entry is not a payout.");
                rig.PlacePlayer(new DVec2(0, 0.5)); rig.Advance(0.04);
                Assert.That(rig.World.TryGetExperience(green, out var orb), Is.True); Assert.That(orb.Position.Local.Y, Is.GreaterThan(0));
                presenter.Refresh(rig.Run, 0); var previous = view.ExperienceViews.Single(x => x.ExperienceId == green).transform.localPosition;
                presenter.Refresh(rig.Run, 1); var current = view.ExperienceViews.Single(x => x.ExperienceId == green).transform.localPosition;
                Assert.That(Vector3.Distance(previous, current), Is.GreaterThan(0));
                rig.Advance(0.4); presenter.Refresh(rig.Run);
                Assert.That(rig.World.TryGetExperience(green, out _), Is.False); Assert.That(rig.Player.Growth.ExperienceIntoLevel, Is.EqualTo(System.Numerics.BigInteger.One));
                Assert.That(view.ExperienceViews.Any(x => x.ExperienceId == green), Is.False);
                Assert.That(rig.World.TryGetExperience(blue, out _), Is.True); Assert.That(rig.World.TryGetExperience(red, out _), Is.True);
                yield return null;
            }
            finally { UnityEngine.Object.Destroy(go); }
        }
        [UnityTest]
        public IEnumerator ARedOrbLeaseCannotMoveOrRecolorTheNextGreenOrb()
        {
            var root = new GameObject("OrbPoolFixture"); var catalog = Resources.Load<GameCatalog>("Bootstrap/GameCatalog");
            try
            {
                using var pool = new ViewPool<ExperienceView>(Guid.NewGuid(), () =>
                { var go = new GameObject("Orb"); go.transform.SetParent(root.transform); var result = go.AddComponent<ExperienceView>(); result.Initialize(catalog); return result; });
                var red = pool.Rent(1); Assert.That(pool.TryGet(red, out var old), Is.True); old.Bind(1, ExperienceTier.Red); old.SetPosition(Vector3.one * 10);
                float redHalo = old.HaloRadius; pool.Return(red);
                var green = pool.Rent(2); Assert.That(pool.TryGet(green, out var fresh), Is.True); fresh.Bind(2, ExperienceTier.Green);
                Assert.That(fresh, Is.SameAs(old)); Assert.That(fresh.HaloRadius, Is.LessThan(redHalo));
                Assert.That(fresh.DisplayColor, Is.EqualTo(catalog.Visuals.ExperienceColor(ExperienceTier.Green)));
                Assert.That(fresh.transform.localPosition, Is.EqualTo(Vector3.zero)); Assert.That(fresh.TrySetPosition(red, Vector3.one * 100), Is.False);
                Assert.That(fresh.ExperienceId, Is.EqualTo(2)); Assert.That(fresh.Tier, Is.EqualTo(ExperienceTier.Green));
                yield return null;
            }
            finally { UnityEngine.Object.Destroy(root); }
        }
    }
}
