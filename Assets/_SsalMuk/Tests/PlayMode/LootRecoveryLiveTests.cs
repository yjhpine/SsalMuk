using System;
using System.Collections;
using NUnit.Framework;
using SsalMuk.Core;
using SsalMuk.Presentation;
using SsalMuk.Unity;
using UnityEngine;
using UnityEngine.TestTools;

namespace SsalMuk.Tests
{
    public sealed class LootRecoveryLiveTests
    {
        [UnityTest]
        public IEnumerator CatalogSpeedHerdIsFlankedAndRememberedLootReachesThePlayer()
        {
            var catalog = Resources.Load<GameCatalog>("Bootstrap/GameCatalog");
            var root = new GameObject("LootRecoveryFixture");
            try
            {
                using var rig = RunTestRig.Create(enableCombat: false, definitions: catalog.Defaults.CreateCatalog(),
                    movementSettings: catalog.Defaults.CreateMovementSettings(), pickupSettings: catalog.Defaults.CreatePickupSettings());
                var view = root.AddComponent<WorldView>(); view.Initialize(catalog); var presenter = new WorldPresenter(view);
                long loot = rig.DropXp(new DVec2(4, 0), 1); rig.Ai.Tick(.02);
                for (int i = 0; i < 9; i++) rig.Spawn(UnitKind.Normal, new DVec2(.7 + i / 3 * .65, (i % 3 - 1) * .65), 1000);
                var collector = new ExperienceCollector(rig.Run, rig.Movement, new ProgressionService(rig.Run));
                bool flanked = false, leftSearchRange = false; double maxY = 0;
                int steps = 0;
                for (; steps < 1500 && rig.World.TryGetExperience(loot, out _); steps++)
                {
                    rig.Advance(.02); collector.Step(.02);
                    var position = WorldPosition.FromLocal(DVec2.Zero).DisplacementTo(rig.Player.Position);
                    maxY = Math.Max(maxY, Math.Abs(position.Y));
                    flanked |= rig.Ai.CollectionTactic == CollectionTactic.Flank;
                    leftSearchRange |= rig.Player.Position.DistanceTo(WorldPosition.FromLocal(new DVec2(4, 0))) > rig.Ai.Settings.SearchDistance;
                    if (steps % 10 == 0) { presenter.Refresh(rig.Run); yield return null; }
                }
                presenter.Refresh(rig.Run);
                Assert.That(flanked, Is.True); Assert.That(leftSearchRange, Is.True);
                Assert.That(maxY, Is.GreaterThan(2));
                Assert.That(rig.World.TryGetExperience(loot, out _), Is.False);
                Assert.That(rig.Player.Growth.ExperienceIntoLevel, Is.EqualTo(System.Numerics.BigInteger.One));
                TestContext.WriteLine($"Current catalog: player={rig.Player.MoveSpeed}, normal={rig.Run.Definitions.GetUnit(UnitKind.Normal).MoveSpeed}; recovered after {steps * .02:F2}s model time. Movement, AI, XP flight/contact and presentation; combat/spawn disabled.");
            }
            finally { UnityEngine.Object.Destroy(root); }
        }
    }
}
