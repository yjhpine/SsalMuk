using System;
using System.Linq;
using NUnit.Framework;
using SsalMuk.Core;
using SsalMuk.Unity;

namespace SsalMuk.Tests.LibraryWallEscape
{
    public sealed class LibraryWallEscapeTests
    {
        private static RunTestRig Rig(bool combat = false)
        {
            var catalog = UnityEngine.Resources.Load<GameCatalog>("Bootstrap/GameCatalog");
            return RunTestRig.Create(terrain: new ThemedChunkGenerator(73,
                new MapSettings(0, maximumBodyRadius: 1.125), MapKind.Library),
                definitions: catalog.CreateDefinitions(), enableCombat: combat);
        }

        [TestCase(26.5, 1)]
        [TestCase(5.5, -1)]
        public void SafeSideExitWinsOverRetreatingTowardWall(double y, int outward)
        {
            using var rig = Rig(); var origin = new DVec2(16, y);
            rig.PlacePlayer(origin);
            long id = rig.Spawn(UnitKind.Normal, origin + new DVec2(0, -outward * 1.2));
            rig.Ai.Tick(.02);
            Assert.That(rig.Player.BrainState, Is.EqualTo(BrainState.Evade));
            TestContext.WriteLine($"Wall escape y={y}, intent={rig.Player.MoveIntent.X},{rig.Player.MoveIntent.Y}");
            Assert.That(rig.Player.MoveIntent.Y * outward, Is.LessThanOrEqualTo(.1),
                "A contact-free side exit exists; do not retreat farther into the wall.");
            var enemy = rig.Unit(id); var relative = rig.Player.Position.DisplacementTo(enemy.Position);
            var travel = (-relative.Normalized * enemy.MoveSpeed - rig.Player.MoveIntent * rig.Player.MoveSpeed) * rig.Ai.Settings.EmergencyContactSeconds;
            Assert.That(CircleContact.Interval(relative, travel, enemy.BodyRadius + rig.Player.BodyRadius, out _, out _), Is.False);
        }

        [TestCase(25.8, -1)]
        [TestCase(6.2, 1)]
        public void QuietPlayerRestoresRoomBeforeReachingWall(double y, int inward)
        {
            using var rig = Rig(); rig.PlacePlayer(new DVec2(16, y)); rig.Ai.Tick(.02);
            Assert.That(rig.Player.MoveIntent.Y * inward, Is.GreaterThan(.1));
        }

        [Test]
        public void CommittedBreakoutTurnsBeforeRunningOutOfWallRoom()
        {
            using var rig = Rig(); var origin = new DVec2(16, 25.1); rig.PlacePlayer(origin);
            for (int i = 0; i < 16; i++)
            {
                if (i >= 3 && i <= 5) continue; // Only the upward corridor starts open.
                double angle = i * Math.PI / 8;
                rig.Spawn(UnitKind.Normal, origin + new DVec2(Math.Cos(angle), Math.Sin(angle)) * 1.4);
            }
            rig.Ai.Tick(.02);
            Assert.That(rig.Player.BrainState, Is.EqualTo(BrainState.Breakout));
            Assert.That(rig.Player.MoveIntent.Y, Is.GreaterThan(.9));
            foreach (var enemy in rig.Run.Units.Where(u => u.Kind != UnitKind.Player).ToArray()) rig.World.Units.Remove(enemy.Id);
            rig.PlacePlayer(new DVec2(16, 26.5)); rig.Ai.Tick(.02);
            Assert.That(rig.Player.BrainState, Is.EqualTo(BrainState.Breakout));
            Assert.That(rig.Player.MoveIntent.Y, Is.LessThanOrEqualTo(.1), "The old heading must not outrank an open interior route.");
        }

        [TestCase(26.5, 1)]
        [TestCase(5.5, -1)]
        public void ContinuousChaseLeavesRoomToTurn(double y, int outward)
        {
            using var rig = Rig(combat: true); var origin = new DVec2(16, y);
            rig.PlacePlayer(origin); rig.Spawn(UnitKind.Normal, origin + new DVec2(0, -outward * 1.4), 1000);
            double minimum = double.PositiveInfinity;
            for (int i = 0; i < 250; i++)
            {
                rig.Advance(.02);
                double clearance = Math.Min(rig.Player.Position.Local.Y - LibraryChunkGenerator.InteriorBottom,
                    LibraryChunkGenerator.InteriorTop - rig.Player.Position.Local.Y) - rig.Player.BodyRadius;
                minimum = Math.Min(minimum, clearance);
            }
            TestContext.WriteLine($"Five-second chase: minimum wall clearance={minimum:0.000}; health={rig.Player.Health}");
            Assert.That(minimum, Is.GreaterThan(1.25), "Do not spend the escape route pressed against the bookcase wall.");
            Assert.That(rig.Player.IsAlive, Is.True);
        }

        [TestCase(28.1)]
        [TestCase(3.9)]
        public void BreakoutDoesNotKeepFollowingWallAfterCrowdClears(double y)
        {
            using var rig = Rig(); var origin = new DVec2(16, y); rig.PlacePlayer(origin);
            for (int i = 2; i < 15; i++)
            {
                double angle = i * Math.PI / 8;
                rig.Spawn(UnitKind.Normal, origin + new DVec2(Math.Cos(angle), Math.Sin(angle)) * 1.4);
            }
            rig.Ai.Tick(.02);
            Assert.That(rig.Player.BrainState, Is.EqualTo(BrainState.Breakout));
            Assert.That(rig.Player.MoveIntent.X, Is.GreaterThan(.9));
            foreach (var enemy in rig.Run.Units.Where(u => u.Kind != UnitKind.Player).ToArray()) rig.World.Units.Remove(enemy.Id);
            rig.Advance(3);
            double clearance = Math.Min(rig.Player.Position.Local.Y - LibraryChunkGenerator.InteriorBottom,
                LibraryChunkGenerator.InteriorTop - rig.Player.Position.Local.Y) - rig.Player.BodyRadius;
            TestContext.WriteLine($"After crowd clears: y={rig.Player.Position.Local.Y}; clearance={clearance}; state={rig.Player.BrainState}");
            Assert.That(clearance, Is.GreaterThan(2.2), "A wall-parallel breakout must return inward once the corridor opens.");
        }
    }
}
