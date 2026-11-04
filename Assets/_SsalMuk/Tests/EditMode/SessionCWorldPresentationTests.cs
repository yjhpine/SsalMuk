using System;
using System.Collections.Generic;
using NUnit.Framework;
using SsalMuk.Core;
using SsalMuk.Presentation;

namespace SsalMuk.Tests
{
    public sealed class SessionCWorldPresentationTests
    {
        [Test]
        public void UnitAndExperienceVisibilityRetainOnlyThePreviousFrameHysteresis()
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            SpawnPair(rig, 21, out long unit, out long experience);
            var view = new RecordingWorldView();
            var presenter = new WorldPresenter(view);
            int[] distances = { 21, 23, 25, 23, 21 };
            bool[] expected = { true, true, false, false, true };

            for (int i = 0; i < distances.Length; i++)
            {
                MovePair(rig, unit, experience, distances[i]);
                presenter.Refresh(rig.Run, 1, 22);
                AssertPairVisibility(view, unit, experience, expected[i]);
            }

            Assert.That(rig.World.Units.Count, Is.EqualTo(2));
            Assert.That(rig.World.Experience.Count, Is.EqualTo(1));
            Assert.That(view.EndFrameCalls, Is.EqualTo(distances.Length));
        }

        [Test]
        public void ANewRunDoesNotInheritVisibilityForReusedUnitAndExperienceIds()
        {
            using var previous = RunTestRig.Create(enableAi: false, enableCombat: false);
            using var next = RunTestRig.Create(enableAi: false, enableCombat: false);
            SpawnPair(previous, 21, out long previousUnit, out long previousExperience);
            SpawnPair(next, 23, out long nextUnit, out long nextExperience);
            Assert.That(next.Run.Id, Is.Not.EqualTo(previous.Run.Id));
            Assert.That(nextUnit, Is.EqualTo(previousUnit));
            Assert.That(nextExperience, Is.EqualTo(previousExperience));
            var view = new RecordingWorldView();
            var presenter = new WorldPresenter(view);

            presenter.Refresh(previous.Run, 1, 22);
            AssertPairVisibility(view, previousUnit, previousExperience, true);
            presenter.Refresh(next.Run, 1, 22);
            Assert.That(view.RunId, Is.EqualTo(next.Run.Id));
            AssertPairVisibility(view, nextUnit, nextExperience, false);

            MovePair(next, nextUnit, nextExperience, 21);
            presenter.Refresh(next.Run, 1, 22);
            AssertPairVisibility(view, nextUnit, nextExperience, true);
            MovePair(next, nextUnit, nextExperience, 23);
            presenter.Refresh(next.Run, 1, 22);
            AssertPairVisibility(view, nextUnit, nextExperience, true);
        }

        [Test]
        public void AnExperienceDisplayExceptionKeepsThePreviouslyCommittedVisibility()
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            SpawnPair(rig, 21, out long oldUnit, out long oldExperience);
            SpawnPair(rig, 25, out long newUnit, out long newExperience);
            var view = new RecordingWorldView();
            var presenter = new WorldPresenter(view);
            presenter.Refresh(rig.Run, 1, 22);
            AssertPairVisibility(view, oldUnit, oldExperience, true);
            AssertPairVisibility(view, newUnit, newExperience, false);

            MovePair(rig, oldUnit, oldExperience, 25);
            MovePair(rig, newUnit, newExperience, 21);
            view.ThrowOnExperienceId = newExperience;
            var failure = Assert.Throws<InvalidOperationException>(() => presenter.Refresh(rig.Run, 1, 22));
            Assert.That(failure.Message, Is.EqualTo(RecordingWorldView.ExperienceFailure));
            AssertPairVisibility(view, newUnit, newExperience, true);
            Assert.That(view.EndFrameCalls, Is.EqualTo(1));

            view.ThrowOnExperienceId = null;
            MovePair(rig, oldUnit, oldExperience, 23);
            MovePair(rig, newUnit, newExperience, 23);
            presenter.Refresh(rig.Run, 1, 22);
            AssertPairVisibility(view, oldUnit, oldExperience, true);
            AssertPairVisibility(view, newUnit, newExperience, false);
        }

        [Test]
        public void AnEndFrameExceptionKeepsTheVisibilityCommittedBeforeTheCallback()
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            SpawnPair(rig, 21, out long oldUnit, out long oldExperience);
            SpawnPair(rig, 25, out long newUnit, out long newExperience);
            var view = new RecordingWorldView();
            var presenter = new WorldPresenter(view);
            presenter.Refresh(rig.Run, 1, 22);
            AssertPairVisibility(view, oldUnit, oldExperience, true);
            AssertPairVisibility(view, newUnit, newExperience, false);

            MovePair(rig, oldUnit, oldExperience, 25);
            MovePair(rig, newUnit, newExperience, 21);
            view.ThrowOnEndFrame = true;
            var failure = Assert.Throws<InvalidOperationException>(() => presenter.Refresh(rig.Run, 1, 22));
            Assert.That(failure.Message, Is.EqualTo(RecordingWorldView.EndFrameFailure));
            AssertPairVisibility(view, newUnit, newExperience, true);
            Assert.That(view.EndFrameCalls, Is.EqualTo(2));

            view.ThrowOnEndFrame = false;
            MovePair(rig, oldUnit, oldExperience, 23);
            MovePair(rig, newUnit, newExperience, 23);
            presenter.Refresh(rig.Run, 1, 22);
            AssertPairVisibility(view, oldUnit, oldExperience, false);
            AssertPairVisibility(view, newUnit, newExperience, true);
        }

        private static void SpawnPair(RunTestRig rig, double distance, out long unit, out long experience)
        {
            var position = new DVec2(distance, 0);
            unit = rig.Spawn(UnitKind.Normal, position);
            experience = rig.DropXp(position, 1);
            Assert.That(rig.World.TryBeginAttraction(rig.Run.Id, experience), Is.True);
        }

        private static void MovePair(RunTestRig rig, long unit, long experience, double distance)
        {
            var position = WorldPosition.FromLocal(new DVec2(distance, 0));
            rig.World.MoveUnit(unit, position);
            Assert.That(rig.World.MoveExperience(rig.Run.Id, experience, position), Is.True);
        }

        private static void AssertPairVisibility(RecordingWorldView view, long unit, long experience, bool expected)
        {
            Assert.That(view.UnitPositions.ContainsKey(unit), Is.EqualTo(expected), "Unit visibility");
            Assert.That(view.ExperiencePositions.ContainsKey(experience), Is.EqualTo(expected), "Experience visibility");
        }

        private sealed class RecordingWorldView : ICombatWorldView
        {
            public const string ExperienceFailure = "Injected experience display failure.";
            public const string EndFrameFailure = "Injected end-frame failure.";
            public readonly Dictionary<long, DVec2> UnitPositions = new Dictionary<long, DVec2>();
            public readonly Dictionary<long, DVec2> ExperiencePositions = new Dictionary<long, DVec2>();
            public Guid RunId;
            public long? ThrowOnExperienceId;
            public bool ThrowOnEndFrame;
            public int EndFrameCalls;

            public void BeginFrame(Guid runId)
            {
                RunId = runId;
                UnitPositions.Clear();
                ExperiencePositions.Clear();
            }

            public void ShowUnit(UnitModel unit, DVec2 relativePosition) => UnitPositions.Add(unit.Id, relativePosition);
            public void ShowExperience(ExperienceRecord experience, ExperienceTier tier, DVec2 relative, double radius)
            {
                ExperiencePositions.Add(experience.Id, relative);
                if (ThrowOnExperienceId == experience.Id) throw new InvalidOperationException(ExperienceFailure);
            }

            public void EndFrame(double elapsedSeconds, int enemyCount)
            {
                EndFrameCalls++;
                if (ThrowOnEndFrame) throw new InvalidOperationException(EndFrameFailure);
            }

            public void SetFrameTime(double time) { }
            public void ShowTerrain(ChunkData chunk, DVec2 relativeOrigin) { }
            public void ShowAttack(AttackShapeSnapshot shape, DVec2 relative) { }
            public void ShowProjectile(ProjectileModel projectile, DVec2 relative) { }
            public void ShowExplosion(ExplosionSnapshot explosion, DVec2 relative, double lifetime) { }
        }
    }
}
