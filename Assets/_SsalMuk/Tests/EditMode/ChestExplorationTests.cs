using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using SsalMuk.Core;
using SsalMuk.Presentation;

namespace SsalMuk.Tests.ChestExploration
{
    public sealed class ChestExplorationTests
    {
        private static IChunkGenerator Map(MapKind map, int seed = 77) => new ThemedChunkGenerator(seed, new MapSettings(.2, maximumBodyRadius: 1.125), map);
        private static RunTestRig Rig(MapKind map = MapKind.Library, bool ai = false)
        {
            var rig = RunTestRig.Create(seed: 77, terrain: Map(map), enableAi: ai);
            rig.PlacePlayer(new DVec2(16.5, 16.5)); rig.Run.Chests.Enable(); return rig;
        }

        [TestCase(MapKind.Forest, 9), TestCase(MapKind.Library, 3)]
        public void ChestsAreDeterministicAccessibleAndPersistAcrossTerrainEviction(MapKind map, int count)
        {
            using var rig = Rig(map); using var other = Rig(map);
            var chests = rig.Run.Chests.Nearby(rig.Player.Position).ToArray();
            Assert.That(chests.Length, Is.EqualTo(count));
            CollectionAssert.AreEqual(chests.Select(c => c.Position), other.Run.Chests.Nearby(other.Player.Position).Select(c => c.Position));
            foreach (var chest in chests)
            {
                Assert.That(rig.World.Query.IsCircleFree(chest.Position, rig.Player.BodyRadius), Is.True);
                var path = rig.Navigation.RequestPath(rig.Player.Position, chest.Position, rig.Player.BodyRadius);
                for (int i = 0; i < 200 && path.Status == PathStatus.Pending; i++) rig.Navigation.Advance(512);
                Assert.That(path.Status, Is.EqualTo(PathStatus.Ready));
            }
            var first = chests[0]; rig.World.MoveUnit(rig.Player.Id, first.Position);
            Assert.That(rig.Run.Chests.TryOpen(first.Id), Is.True);
            rig.World.ClearTerrainCache(); rig.Run.Chests.Step();
            Assert.That(first.Opened, Is.True);
            Assert.That(rig.Run.Chests.Nearby(first.Position).Single(c => c.Id == first.Id), Is.SameAs(first));
            Assert.That(rig.Run.Chests.TryOpen(first.Id), Is.False);
        }

        [Test]
        public void ChestAddsExactlyOneChoiceWithoutChangingLevelOrExperience()
        {
            using var rig = Rig(); var chest = rig.Run.Chests.Nearby(rig.Player.Position).First();
            var level = rig.Player.Level; var pending = rig.Player.Growth.PendingChoices;
            Assert.That(rig.Run.Chests.TryOpen(chest.Id), Is.False, "Remote opening must be rejected.");
            rig.World.MoveUnit(rig.Player.Id, chest.Position);
            Assert.That(rig.Run.Chests.TryOpen(chest.Id), Is.True);
            rig.Advance(.02); var offer = rig.CurrentOffer;
            Assert.That(offer, Is.Not.Null); Assert.That(offer.Choices.Count, Is.EqualTo(3));
            Assert.That(rig.Player.Level, Is.EqualTo(level)); Assert.That(rig.Player.Growth.PendingChoices, Is.EqualTo(pending));
            Assert.That(rig.Choose(offer.Id, 0), Is.True); Assert.That(rig.Choose(offer.Id, 1), Is.False);
            rig.Advance(.02); Assert.That(rig.CurrentOffer, Is.Null);
            Assert.That(rig.Run.Chests.TryOpen(chest.Id), Is.False);
        }

        [Test]
        public void PausedAndDeadRunsCannotOpenOrApplyChestsAndRestartResetsThem()
        {
            using var rig = Rig(); var chest = rig.Run.Chests.Nearby(rig.Player.Position).First();
            rig.World.MoveUnit(rig.Player.Id, chest.Position); rig.Coordinator.Pause();
            Assert.That(rig.Run.Chests.TryOpen(chest.Id), Is.False);
            rig.Coordinator.Resume(); Assert.That(rig.Run.Chests.TryOpen(chest.Id), Is.True); rig.Advance(.02);
            Assert.That(rig.Choose(rig.CurrentOffer.Id, 0), Is.True); rig.Coordinator.Pause(); rig.Advance(.02);
            Assert.That(rig.CurrentOffer, Is.Not.Null); rig.Coordinator.Resume();
            rig.Hit(rig.Player.Id, 1000); rig.Advance(.02); Assert.That(rig.CurrentOffer, Is.Null);
            Assert.That(rig.Run.Chests.TryOpen(chest.Id), Is.False);
            rig.RestartAsync().GetAwaiter().GetResult(); rig.PlacePlayer(new DVec2(16.5, 16.5)); rig.Run.Chests.Enable();
            Assert.That(rig.Run.Chests.Nearby(rig.Player.Position).All(c => !c.Opened), Is.True);
        }

        [Test]
        public void LevelUpOfferStaysStableAndChestDoesNotConsumeItsChoice()
        {
            using var rig = Rig(); rig.GrantExperience(5); rig.Advance(.02); var levelOffer = rig.CurrentOffer;
            var chest = rig.Run.Chests.Nearby(rig.Player.Position).First(); rig.World.MoveUnit(rig.Player.Id, chest.Position);
            Assert.That(rig.Run.Chests.TryOpen(chest.Id), Is.True); rig.Advance(.02);
            Assert.That(rig.CurrentOffer, Is.SameAs(levelOffer));
            rig.Choose(levelOffer.Id, 0); rig.Advance(.02);
            Assert.That(rig.CurrentOffer, Is.Not.Null); Assert.That(rig.CurrentOffer.Id, Is.GreaterThan(levelOffer.Id));
            Assert.That(rig.Player.Growth.PendingChoices.IsZero, Is.True);
            rig.Choose(rig.CurrentOffer.Id, 0); rig.Advance(.02); Assert.That(rig.CurrentOffer, Is.Null);
        }

        [Test]
        public void AiKeepsChestAsDestinationRatherThanChasingDistantExperienceBehindIt()
        {
            using var rig = Rig(ai: true);
            var chest = rig.Run.Chests.Nearby(rig.Player.Position).OrderBy(c => c.Position.DistanceTo(rig.Player.Position)).First();
            var delta = rig.Player.Position.DisplacementTo(chest.Position);
            var xp = rig.Player.Position.Offset(-delta.Normalized * 8);
            rig.World.AddExperience(xp, 1000);
            rig.Ai.Tick(.02);
            Assert.That(rig.Run.Chests.Target, Is.SameAs(chest));
            Assert.That(DVec2.Dot(rig.Player.MoveIntent, delta.Normalized), Is.GreaterThan(0));
            rig.Advance(30);
            Assert.That(chest.Opened, Is.True);
        }

        [Test]
        public void AiCollectsNearbyExperienceAlongTheWayToChest()
        {
            // Flat ground isolates the intended opportunistic turn from furniture and enemy avoidance.
            using var rig = RunTestRig.Create(seed: 77);
            rig.PlacePlayer(new DVec2(16.5, 16.5)); rig.Run.Chests.Enable();
            rig.Ai.Tick(.02); var chest = rig.Run.Chests.Target; Assert.That(chest, Is.Not.Null);
            var toward = rig.Player.Position.DisplacementTo(chest.Position).Normalized;
            var side = new DVec2(-toward.Y, toward.X);
            double pickup = rig.Run.PickupSettings.AttractionRadius;
            var xpPosition = rig.Player.Position.Offset(toward * 1.5 + side * (pickup + .65));
            long xp = rig.World.AddExperience(xpPosition, 2);
            rig.Advance(.22);
            Assert.That(DVec2.Dot(rig.Player.MoveIntent, side), Is.GreaterThan(.1), "Near-path XP should influence the direction.");
            Assert.That(DVec2.Dot(rig.Player.MoveIntent, toward), Is.GreaterThan(0), "Still make progress toward the chest.");
            rig.Advance(30);
            Assert.That(rig.World.TryGetExperience(xp, out _), Is.False);
            Assert.That(chest.Opened, Is.True);
        }

        [Test]
        public void EmergencyInterruptsChestApproachThenReturnsToChest()
        {
            using var rig = Rig(ai: true); rig.Ai.Tick(.02);
            var chest = rig.Run.Chests.Target; Assert.That(chest, Is.Not.Null);
            var danger = rig.Player.Position.Local + rig.Player.MoveIntent.Normalized * .75;
            long enemy = rig.Spawn(UnitKind.Normal, danger);
            rig.Ai.Tick(.02); Assert.That(rig.Player.BrainState, Is.Not.EqualTo(BrainState.Collect));
            rig.World.Units.Remove(enemy); rig.Advance(30);
            Assert.That(chest.Opened, Is.True);
        }

        [Test]
        public void ChestSelectionDoesNotAutomaticallyApplyAfterFiveSeconds()
        {
            using var rig = Rig(); var chest = rig.Run.Chests.Nearby(rig.Player.Position).First();
            rig.World.MoveUnit(rig.Player.Id, chest.Position); rig.Run.Chests.TryOpen(chest.Id); rig.Advance(.02);
            var view = new View(); using var presenter = new LevelUpPresenter(view);
            presenter.Refresh(rig.Run); rig.Advance(6); presenter.Refresh(rig.Run);
            Assert.That(rig.Run.Rewards.HasQueuedChoice, Is.False);
            Assert.That(view.Remaining, Does.Contain("직접 선택"));
            view.Submit(0); rig.Advance(.02); Assert.That(rig.CurrentOffer, Is.Null);
        }

        [Test]
        public void FastPlayerNavigatesAroundFurnitureWithoutWaypointOscillation()
        {
            using var rig = Rig(ai: true);
            for (int i = 0; i < 70; i++) new GrowthService(rig.Run).UpgradeShared(SharedUpgradeKind.MoveSpeed);
            WorldPosition? start = null; ChestRecord target = null;
            for (int y = 5; y < 27 && !start.HasValue; y++) for (int x = 5; x < 27 && !start.HasValue; x++)
            {
                var position = WorldPosition.FromLocal(new DVec2(x + .2, y + .2));
                if (!rig.World.Query.IsCircleFree(position, rig.Player.BodyRadius)) continue;
                var closest = rig.Run.Chests.Nearby(position).OrderBy(c => c.Position.DistanceTo(position)).First();
                if (position.DistanceTo(closest.Position) > 3 && rig.World.Query.SweepCircle(position, position.DisplacementTo(closest.Position), rig.Player.BodyRadius).HasValue)
                { start = position; target = closest; }
            }
            Assert.That(start.HasValue, Is.True, "Fixture must need navigation around furniture.");
            rig.World.MoveUnit(rig.Player.Id, start.Value);
            for (int i = 0; i < 600; i++)
            {
                rig.Advance(.02);
                if (i % 100 == 0) TestContext.WriteLine($"Fast chest route: player={rig.Player.Position.Local.X},{rig.Player.Position.Local.Y}; intent={rig.Player.MoveIntent.X},{rig.Player.MoveIntent.Y}; target={rig.Run.Chests.Target?.Position.Local.X},{rig.Run.Chests.Target?.Position.Local.Y}");
            }
            Assert.That(target.Opened, Is.True, "A fast player must not bounce forever over a path waypoint.");
        }

        private sealed class View : ILevelUpView
        {
            public event Action<int> ChoiceRequested;
            public string Remaining;
            public void Submit(int slot) => ChoiceRequested?.Invoke(slot);
            public void Show(bool visible, bool canSubmit, string remaining, IReadOnlyList<string> titles, IReadOnlyList<string> descriptions) => Remaining = remaining;
        }
    }
}
