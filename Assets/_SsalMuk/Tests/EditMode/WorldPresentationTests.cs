using System;
using System.Collections.Generic;
using NUnit.Framework;
using SsalMuk.Core;
using SsalMuk.Presentation;

namespace SsalMuk.Tests
{
    public sealed class WorldPresentationTests
    {
        [Test]
        public void PresenterUsesRelativeCoordinatesAndReclaimsOnlyViewsWithHysteresis()
        {
            using var rig = RunTestRig.Create();
            Assert.That(rig.Run.Phase, Is.EqualTo(RunPhase.Running));
            var origin = new WorldPosition(new ChunkCoord(9007199254740993, -7), new DVec2(16, 16));
            rig.World.MoveUnit(rig.Player.Id, origin);
            var near = WorldStoreTests.Spawn(rig.World, UnitKind.Normal, origin.Offset(new DVec2(20, 0)));
            var far = WorldStoreTests.Spawn(rig.World, UnitKind.Normal, origin.Offset(new DVec2(2000, 0)));
            var view = new ObservedWorldView(); var presenter = new WorldPresenter(view);
            presenter.Refresh(rig.Run);
            Assert.That(view.Positions[near.Id], Is.EqualTo(new DVec2(20, 0)));
            Assert.That(view.Positions.ContainsKey(far.Id), Is.False);
            rig.World.MoveUnit(near.Id, origin.Offset(new DVec2(23, 0))); presenter.Refresh(rig.Run);
            Assert.That(view.Positions.ContainsKey(near.Id), Is.True);
            rig.World.MoveUnit(near.Id, origin.Offset(new DVec2(25, 0))); presenter.Refresh(rig.Run);
            Assert.That(view.Positions.ContainsKey(near.Id), Is.False);
            Assert.That(rig.World.Units.Count, Is.EqualTo(3)); Assert.That(view.EnemyCount, Is.EqualTo(2));
            Assert.That(rig.World.Units.Get(far.Id).Position, Is.EqualTo(origin.Offset(new DVec2(2000, 0))));
        }
        private sealed class ObservedWorldView : IWorldView
        {
            public readonly Dictionary<long, DVec2> Positions = new Dictionary<long, DVec2>(); public int EnemyCount;
            public void BeginFrame(Guid runId) => Positions.Clear();
            public void ShowTerrain(ChunkData chunk, DVec2 origin) { }
            public void ShowUnit(UnitModel unit, DVec2 position) => Positions[unit.Id] = position;
            public void EndFrame(double seconds, int count) => EnemyCount = count;
        }
    }
}
