using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class RunCoordinatorTests
    {
        [TestCase(WeaponKind.Sword), TestCase(WeaponKind.Spear), TestCase(WeaponKind.Axe), TestCase(WeaponKind.Fireball)]
        public async Task StartingWeaponIsChosenBeforePlayerCreation(WeaponKind kind)
        {
            using var coordinator = new RunCoordinator(new GateLoader { Immediate = true }, () => new ObservedBuilder());
            await coordinator.StartRunAsync(kind);
            Assert.That(coordinator.Run.Player.Weapons.Kinds, Is.EqualTo(new[] { kind }));
            Assert.That(coordinator.Run.Player.Weapons.Get(kind).GetLevel(UpgradeKind.Damage), Is.EqualTo(BigInteger.Zero));
        }
        [Test]
        public async Task RepeatedStartDuringLoadCannotChangeTheChosenWeapon()
        {
            var loader = new GateLoader(); using var coordinator = new RunCoordinator(loader, () => new ObservedBuilder());
            var first = coordinator.StartRunAsync(WeaponKind.Fireball);
            Assert.That(coordinator.StartRunAsync(WeaponKind.Axe), Is.SameAs(first));
            loader.Release(); await first;
            Assert.That(coordinator.Run.Player.Weapons.Kinds, Is.EqualTo(new[] { WeaponKind.Fireball }));
            await coordinator.ReturnToMenuAsync(); await coordinator.StartRunAsync(WeaponKind.Spear);
            Assert.That(coordinator.Run.Player.Weapons.Kinds, Is.EqualTo(new[] { WeaponKind.Spear }));
        }
        [Test]
        public void InvalidStartingWeaponIsRejectedBeforeLoading()
        {
            int builds = 0;
            using var coordinator = new RunCoordinator(new GateLoader(), () => { builds++; return new ObservedBuilder(); });
            Assert.Throws<ArgumentOutOfRangeException>(() => coordinator.StartRunAsync((WeaponKind)999));
            Assert.That(coordinator.Phase, Is.EqualTo(RunPhase.MainMenu)); Assert.That(builds, Is.Zero);
        }
        [Test]
        public void PauseFreezesSimulationAndPreservesQueuedUpgradeUntilResume()
        {
            using var rig = RunTestRig.Create(scheduledSpawns: true);
            rig.Spawn(UnitKind.Normal, new DVec2(1.4, 0), 10000);
            rig.GrantExperience(5); rig.Advance(.2);
            var offer = rig.CurrentOffer; Assert.That(offer, Is.Not.Null);
            Assert.That(rig.Choose(offer.Id, 0), Is.True);
            double time = rig.Clock.ElapsedSeconds, health = rig.Player.Health;
            var positions = rig.World.Units.Units.ToDictionary(unit => unit.Id, unit => unit.Position);
            long attacks = rig.Simulation.Sword.LaunchCount; var pending = rig.Player.Growth.PendingChoices;
            Assert.That(rig.Coordinator.Pause(), Is.True); Assert.That(rig.Coordinator.Pause(), Is.False);
            rig.Advance(2); rig.Run.Rewards.Step(); rig.Run.Rewards.RefreshOffer();
            Assert.That(rig.Clock.IsRunning, Is.False); Assert.That(rig.Clock.ElapsedSeconds, Is.EqualTo(time));
            Assert.That(rig.Player.Health, Is.EqualTo(health)); Assert.That(rig.Simulation.Sword.LaunchCount, Is.EqualTo(attacks));
            Assert.That(rig.World.Units.Count, Is.EqualTo(positions.Count));
            foreach (var unit in rig.World.Units.Units) Assert.That(unit.Position, Is.EqualTo(positions[unit.Id]));
            Assert.That(rig.CurrentOffer, Is.SameAs(offer)); Assert.That(rig.Run.Rewards.HasQueuedChoice, Is.True);
            Assert.That(rig.Player.Growth.PendingChoices, Is.EqualTo(pending));
            Assert.That(rig.Coordinator.Resume(), Is.True); Assert.That(rig.Coordinator.Resume(), Is.False);
            rig.Advance(.02);
            Assert.That(rig.Clock.ElapsedSeconds, Is.EqualTo(time + .02).Within(1e-9));
            Assert.That(rig.Player.Growth.PendingChoices, Is.EqualTo(pending - 1));
        }
        [Test]
        public async Task PausedRunRejectsNewRewardInputAndMenuDoesNotInheritPause()
        {
            using var rig = RunTestRig.Create(enableAi: false); rig.GrantExperience(5); rig.Advance(.02);
            rig.Coordinator.Pause(); Assert.That(rig.Choose(rig.CurrentOffer.Id, 0), Is.False);
            await rig.Coordinator.ReturnToMenuAsync();
            Assert.That(rig.Coordinator.Pause(), Is.False); Assert.That(rig.Coordinator.Resume(), Is.False);
            await rig.Coordinator.StartRunAsync();
            Assert.That(rig.Run.IsPaused, Is.False); Assert.That(rig.Clock.IsRunning, Is.True);
        }
        [Test]
        public async Task DoubleStartWaitsForSceneAndBuildsOneRunInOrder()
        {
            var loader = new GateLoader(); var builder = new ObservedBuilder(); int scopes = 0;
            using var coordinator = new RunCoordinator(loader, () => { scopes++; return builder; });
            var phases = new List<RunPhase>(); coordinator.PhaseChanged += phases.Add;
            Task first = coordinator.StartRunAsync(); Task second = coordinator.StartRunAsync();
            Assert.That(second, Is.SameAs(first));
            Assert.That(coordinator.Phase, Is.EqualTo(RunPhase.Loading));
            Assert.That(builder.Run.Clock.IsRunning, Is.False); Assert.That(builder.Run.World.Units.Count, Is.Zero);
            loader.Release(); await first;
            CollectionAssert.AreEqual(new[] { RunPhase.Loading, RunPhase.BuildingWorld, RunPhase.CreatingPlayer, RunPhase.CreatingEnemies, RunPhase.Running }, phases);
            CollectionAssert.AreEqual(new[] { "world", "player", "enemies" }, builder.Stages);
            Assert.That(scopes, Is.EqualTo(1)); Assert.That(builder.Run.Clock.IsRunning, Is.True);
            Assert.That(builder.Run.Clock.ElapsedTicks, Is.Zero);
            await coordinator.StartRunAsync(); Assert.That(scopes, Is.EqualTo(1));
        }

        [TestCase("load")] [TestCase("world")] [TestCase("player")] [TestCase("enemies")]
        public async Task FailedStartDisposesPartialWorldAndReturnsToMenu(string failure)
        {
            var loader = new GateLoader(); var builder = new ObservedBuilder { Failure = failure };
            using var coordinator = new RunCoordinator(loader, () => builder);
            Task task = coordinator.StartRunAsync();
            if (failure == "load") loader.Fail(); else loader.Release();
            await task;
            Assert.That(coordinator.Phase, Is.EqualTo(RunPhase.MainMenu));
            Assert.That(coordinator.LastError, Is.Not.Empty);
            Assert.That(builder.DisposeCount, Is.EqualTo(1));
            Assert.That(builder.Run.Clock.IsRunning, Is.False);
            Assert.That(builder.Run.World.Units.Count, Is.Zero);
            Assert.That(coordinator.Run, Is.Null);
        }

        [Test]
        public async Task DisposalWhileLoadingCannotStartALateRun()
        {
            var loader = new GateLoader(); var builder = new ObservedBuilder();
            var coordinator = new RunCoordinator(loader, () => builder);
            Task pending = coordinator.StartRunAsync(); coordinator.Dispose(); loader.Release(); await pending;
            Assert.That(coordinator.Phase, Is.EqualTo(RunPhase.Disposed));
            Assert.That(builder.DisposeCount, Is.EqualTo(1)); Assert.That(builder.Stages, Is.Empty);
            Assert.That(builder.Run.Clock.IsRunning, Is.False);
        }

        [Test]
        public async Task ACancelledOldSceneCompletionCannotReplaceTheNewRun()
        {
            var loader = new GateLoader(); var first = new ObservedBuilder(); var second = new ObservedBuilder(); int attempts = 0;
            using var coordinator = new RunCoordinator(loader, () => attempts++ == 0 ? first : second);
            Task late = coordinator.StartRunAsync(); await coordinator.ReturnToMenuAsync();
            Assert.That(first.DisposeCount, Is.EqualTo(1));
            loader.Immediate = true; await coordinator.StartRunAsync(); var current = coordinator.Run.Id;
            loader.Release(); await late;
            Assert.That(coordinator.Run.Id, Is.EqualTo(current)); Assert.That(coordinator.Run, Is.SameAs(second.Run));
            Assert.That(coordinator.Phase, Is.EqualTo(RunPhase.Running)); Assert.That(first.Stages, Is.Empty);
            Assert.That(second.Stages.Count, Is.EqualTo(3)); Assert.That(first.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public async Task RetryUsesFreshRunIdentityAndDefinitionsStayUnchanged()
        {
            var loader = new GateLoader(); var first = new ObservedBuilder { Failure = "player" }; var second = new ObservedBuilder();
            int attempts = 0;
            using var coordinator = new RunCoordinator(loader, () => attempts++ == 0 ? first : second);
            var task = coordinator.StartRunAsync(); loader.Release(); await task; await coordinator.StartRunAsync();
            Assert.That(coordinator.Phase, Is.EqualTo(RunPhase.Running));
            Assert.That(coordinator.Run.Id, Is.Not.EqualTo(first.Run.Id));
            Assert.That(coordinator.Run.Player.Weapons.Kinds, Is.EquivalentTo(new[] { WeaponKind.Sword }));
            Assert.That(coordinator.Run.Player.Health, Is.EqualTo(coordinator.Run.Definitions.GetUnit(UnitKind.Player).MaxHealth));
        }

        private sealed class GateLoader : ISceneLoader
        {
            private readonly TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>();
            public bool Immediate;
            public Task LoadBattleAsync() => Immediate ? Task.CompletedTask : completion.Task;
            public Task LoadMainMenuAsync() => Task.CompletedTask;
            public void Release() => completion.TrySetResult(true);
            public void Fail() => completion.TrySetException(new InvalidOperationException("Scene load failed."));
        }
        private sealed class ObservedBuilder : IRunBuilder
        {
            public RunModel Run { get; } = new RunModel(Guid.NewGuid(), 7, Definitions(), new ChunkGenerator(7, MapSettings.TestDefaults(0)));
            public readonly List<string> Stages = new List<string>();
            public string Failure; public int DisposeCount;
            public void BuildWorld() { Stage("world"); Run.World.GetChunk(default); }
            public void CreatePlayer()
            {
                Stage("player");
                Run.SetPlayer((PlayerModel)new PlayerFactory(Run.World.Units).Spawn(new UnitSpawnRequest(Run.Id, UnitKind.Player, default, Run.Definitions.GetUnit(UnitKind.Player))));
            }
            public void CreateInitialEnemies()
            {
                Stage("enemies"); new GroundEnemyFactory(Run.World.Units).Spawn(new UnitSpawnRequest(Run.Id, UnitKind.Normal, WorldPosition.FromLocal(new DVec2(5, 0)), Run.Definitions.GetUnit(UnitKind.Normal)));
            }
            private void Stage(string stage)
            {
                Assert.That(Run.Clock.IsRunning, Is.False); Stages.Add(stage);
                if (Failure == stage) throw new InvalidOperationException(stage + " failed.");
            }
            public void Dispose() { DisposeCount++; Run.Dispose(); }
            private static DefinitionCatalog Definitions() => new DefinitionCatalog(Enum.GetValues(typeof(UnitKind)).Cast<UnitKind>().Select(kind => new UnitDefinition(kind.ToString(), kind, 10, 2, 0.26, 1, BigInteger.One)));
        }
    }
}
