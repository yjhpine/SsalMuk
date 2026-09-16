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
    public sealed class PersistentWorldViewTests
    {
        [UnityTest]
        public IEnumerator ViewsAreReusedWhileFarUnitsLootAndInFlightAttractionKeepTheirLogic()
        {
            var go = new GameObject("PersistenceViewFixture"); var catalog = Resources.Load<GameCatalog>("Bootstrap/GameCatalog");
            try
            {
                using var rig = RunTestRig.Create(enableAi: false); var view = go.AddComponent<WorldView>(); view.Initialize(catalog); var presenter = new WorldPresenter(view);
                long ground = rig.Spawn(UnitKind.Normal, new DVec2(5, 1), 1000), air = rig.Spawn(UnitKind.Air, new DVec2(10, 1), 1000);
                long loot = rig.DropXp(new DVec2(4, 0), 25), flying = rig.DropXp(new DVec2(1.2, 0), 1);
                rig.Hit(ground, 1); rig.Advance(0.02); presenter.Refresh(rig.Run);
                var old = view.UnitViews.Single(x => x.UnitId == ground); var oldToken = old.Lease;
                Assert.That(old.Body.color, Is.EqualTo(Color.red)); Assert.That(rig.World.TryGetExperience(flying, out var moving), Is.True);
                var flightBefore = moving.Position; var airBefore = rig.Unit(air).Position;
                rig.PlacePlayer(new DVec2(3200, 3200)); presenter.Refresh(rig.Run); Assert.That(view.VisibleUnitCount, Is.EqualTo(1));
                Assert.That(view.VisibleExperienceCount, Is.Zero); Assert.That(old.Accepts(oldToken), Is.False);
                rig.Advance(1); presenter.Refresh(rig.Run);
                Assert.That(rig.Unit(ground).Health, Is.EqualTo(999)); Assert.That(rig.Unit(air).Health, Is.EqualTo(1000));
                Assert.That(airBefore.DistanceTo(rig.Unit(air).Position), Is.EqualTo(6).Within(1e-7));
                Assert.That(rig.World.TryGetExperience(loot, out var kept), Is.True); Assert.That(kept.Value, Is.EqualTo(new System.Numerics.BigInteger(25)));
                Assert.That(rig.World.TryGetExperience(flying, out moving), Is.True); Assert.That(moving.State, Is.EqualTo(ExperienceState.Attracting));
                Assert.That(flightBefore.DistanceTo(moving.Position), Is.GreaterThan(5)); Assert.That(rig.Player.Growth.ExperienceIntoLevel, Is.EqualTo(System.Numerics.BigInteger.Zero));
                rig.PlacePlayer(new DVec2(0, 0)); presenter.Refresh(rig.Run);
                Assert.That(view.UnitViews.Any(x => x.UnitId == ground), Is.True); Assert.That(view.ExperienceViews.Any(x => x.ExperienceId == loot), Is.True);
                Assert.That(view.UnitViews.Single(x => x.UnitId == ground).Body.color, Is.EqualTo(Color.white));
                rig.Advance(2); presenter.Refresh(rig.Run);
                Assert.That(rig.World.TryGetExperience(flying, out _), Is.False); Assert.That(rig.Player.Growth.ExperienceIntoLevel, Is.EqualTo(System.Numerics.BigInteger.One));
                yield return null;
            }
            finally { UnityEngine.Object.Destroy(go); }
        }
    }
}
