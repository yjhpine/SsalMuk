using System;
using System.Collections.Generic;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class NavigationTests
    {
        [Test]
        public void BudgetedPathStaysPendingThenRoutesAroundWall()
        {
            using var world = Layout((x, y) => x == 5 && y >= 1 && y <= 8);
            var navigation = new NavigationService(world);
            var from = P(2.5, 4.5); var to = P(9.5, 4.5);
            var path = navigation.RequestPath(from, to, 0.26);
            navigation.Advance(1);
            Assert.That(path.Status, Is.EqualTo(PathStatus.Pending));
            Complete(navigation, path);
            Assert.That(path.Status, Is.EqualTo(PathStatus.Ready));
            Assert.That(path.Waypoints.Count, Is.GreaterThan(2));
            AssertSafe(world, from, path.Waypoints, 0.26);
            Assert.That(path.Waypoints[path.Waypoints.Count - 1], Is.EqualTo(to));
        }

        [Test]
        public void CornerCuttingAndTooNarrowPassagesAreRejected()
        {
            using var corner = Layout((x, y) => (x == 3 && y == 2) || (x == 2 && y == 3));
            var navigation = new NavigationService(corner);
            var path = navigation.RequestPath(P(2.5, 2.5), P(3.5, 3.5), 0.26);
            Complete(navigation, path);
            Assert.That(path.Status, Is.EqualTo(PathStatus.Ready));
            AssertSafe(corner, P(2.5, 2.5), path.Waypoints, 0.26);
            Assert.That(path.Waypoints.Count, Is.GreaterThan(2));
            using var corridor = Layout((x, y) => y == 3 || y == 5);
            var normal = new NavigationService(corridor);
            var small = normal.RequestPath(P(2.5, 4.5), P(8.5, 4.5), 0.26);
            var boss = normal.RequestPath(P(2.5, 4.5), P(8.5, 4.5), 0.75);
            Complete(normal, small); Complete(normal, boss);
            Assert.That(small.Status, Is.EqualTo(PathStatus.Ready));
            Assert.That(boss.Status, Is.EqualTo(PathStatus.NoPath));
        }

        [Test]
        public void UnreachableTargetNeverBecomesAStraightLineAndRiskCostChangesRoute()
        {
            using var enclosed = Layout((x, y) => ((x == 7 || x == 9) && y >= 7 && y <= 9) || ((y == 7 || y == 9) && x >= 7 && x <= 9));
            var navigation = new NavigationService(enclosed);
            var path = navigation.RequestPath(P(3.5, 8.5), P(8.5, 8.5), 0.26);
            Complete(navigation, path);
            Assert.That(path.Status, Is.EqualTo(PathStatus.NoPath)); Assert.That(path.Waypoints, Is.Empty);
            using var open = Layout((x, y) => false);
            var weighted = new NavigationService(open, cell => cell.Y == 4 && cell.X >= 4 && cell.X <= 8 ? 100 : 0);
            var safe = weighted.RequestPath(P(2.5, 4.5), P(10.5, 4.5), 0.26);
            Complete(weighted, safe);
            Assert.That(safe.Status, Is.EqualTo(PathStatus.Ready));
            foreach (var point in safe.Waypoints)
                Assert.That(point.Local.Y != 4.5 || point.Local.X < 4 || point.Local.X > 9, Is.True);
            var invalid = new NavigationService(open, cell => -1);
            Assert.Throws<ArgumentOutOfRangeException>(() => { var request = invalid.RequestPath(P(2.5, 2.5), P(3.5, 2.5), 0.26); Complete(invalid, request); });
        }

        [Test]
        public void FlowFieldsAreSharedByRadiusAndRegeneratedAfterTargetOrTerrainChanges()
        {
            using var world = Layout((x, y) => x == 8 && y > 3 && y < 15);
            var navigation = new NavigationService(world);
            var field = navigation.GetFlowField(P(16.5, 16.5), 0.26);
            Assert.That(navigation.GetFlowField(P(16.9, 16.2), 0.26), Is.SameAs(field));
            Assert.That(navigation.GetFlowField(P(16.5, 16.5), 0.75), Is.Not.SameAs(field));
            for (int i = 0; i < 200 && !field.IsComplete; i++) navigation.Advance(256);
            Assert.That(field.TryGetNext(GridCell.At(P(6.5, 8.5)), out var next), Is.True);
            Assert.That(world.Query.IsCircleFree(next.Center, 0.26), Is.True);
            Assert.That(navigation.GetFlowField(P(17.5, 16.5), 0.26), Is.Not.SameAs(field));
            world.ClearTerrainCache();
            Assert.That(navigation.GetFlowField(P(16.5, 16.5), 0.26), Is.Not.SameAs(field));
        }

        [Test]
        public void FarChunkRouteSurvivesCacheEviction()
        {
            using var world = new WorldStore(new UnitRegistry(Guid.NewGuid()), new ChunkGenerator(1234, MapSettings.TestDefaults()));
            var navigation = new NavigationService(world);
            var start = new WorldPosition(new ChunkCoord(-4, 2), new DVec2(16.5, 16.5));
            var target = P(16.5, 16.5);
            var path = navigation.RequestPath(start, target, 0.75); Complete(navigation, path);
            Assert.That(path.Status, Is.EqualTo(PathStatus.Ready));
            world.ClearTerrainCache(); AssertSafe(world, start, path.Waypoints, 0.75);
        }

        internal static WorldPosition P(double x, double y) => WorldPosition.FromLocal(new DVec2(x, y));
        internal static WorldStore Layout(Func<int, int, bool> blocked) => new WorldStore(new UnitRegistry(Guid.NewGuid()), new LayoutGenerator(blocked));
        internal static void Complete(NavigationService navigation, PathRequest request)
        { for (int i = 0; i < 1000 && request.Status == PathStatus.Pending; i++) navigation.Advance(256); }
        internal static void AssertSafe(WorldStore world, WorldPosition start, IReadOnlyList<WorldPosition> path, double radius)
        {
            foreach (var next in path)
            {
                Assert.That(world.Query.IsCircleFree(next, radius), Is.True);
                Assert.That(world.Query.SweepCircle(start, start.DisplacementTo(next), radius), Is.Null);
                start = next;
            }
        }
        private sealed class LayoutGenerator : IChunkGenerator
        {
            private readonly Func<int, int, bool> blocked;
            public LayoutGenerator(Func<int, int, bool> blocked) { this.blocked = blocked; }
            public ChunkData Generate(ChunkCoord coord)
            {
                var cells = new bool[1024];
                if (coord.Equals(default(ChunkCoord))) for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) cells[y * 32 + x] = blocked(x, y);
                return new ChunkData(coord, cells, 1);
            }
        }
    }
}
