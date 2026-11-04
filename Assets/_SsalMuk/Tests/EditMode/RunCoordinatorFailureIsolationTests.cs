using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class RunCoordinatorFailureIsolationTests
    {
        [Test]
        public async Task CancelledFailureRecoveryCannotSetAnErrorOnTheNewRunningSession()
        {
            var staleMenuLoad = new TaskCompletionSource<bool>();
            var currentMenuLoad = new TaskCompletionSource<bool>();
            var loader = new QueuedLoader();
            loader.BattleLoads.Enqueue(Task.FromException(new InvalidOperationException("First battle load failed.")));
            loader.BattleLoads.Enqueue(Task.CompletedTask);
            loader.MenuLoads.Enqueue(staleMenuLoad.Task);
            loader.MenuLoads.Enqueue(currentMenuLoad.Task);
            var builders = new List<SessionBuilder>();
            using var coordinator = new RunCoordinator(loader, () =>
            {
                var builder = new SessionBuilder();
                builders.Add(builder);
                return builder;
            });

            Task failedStart = coordinator.StartRunAsync();
            Assert.That(failedStart.IsCompleted, Is.False);
            Assert.That(coordinator.Phase, Is.EqualTo(RunPhase.Loading));
            Assert.That(coordinator.LastError, Does.Contain("First battle load failed."));
            Assert.That(coordinator.Run, Is.Null);
            Assert.That(builders[0].DisposeCount, Is.EqualTo(1));

            Task returnToMenu = coordinator.ReturnToMenuAsync();
            Assert.That(returnToMenu, Is.Not.SameAs(failedStart));
            Assert.That(returnToMenu.IsCompleted, Is.False);
            Assert.That(loader.MenuLoads, Is.Empty, "The explicit return must request its own menu load.");
            currentMenuLoad.SetResult(true);
            await CompleteWithinTimeout(returnToMenu);
            Assert.That(coordinator.Phase, Is.EqualTo(RunPhase.MainMenu));

            await CompleteWithinTimeout(coordinator.StartRunAsync(WeaponKind.Spear));
            var current = coordinator.Run;
            Assert.That(coordinator.Phase, Is.EqualTo(RunPhase.Running));
            Assert.That(current.Id, Is.Not.EqualTo(builders[0].Run.Id));
            Assert.That(coordinator.LastError, Is.Empty);

            // The cancelled start is still waiting for its own recovery scene load.
            // At baseline dcda7a0 its catch appends this stale error before checking the generation;
            // the final LastError assertion records the expected failing isolation contract.
            staleMenuLoad.SetException(new InvalidOperationException("Stale menu load failed."));
            await CompleteWithinTimeout(failedStart);

            Assert.That(coordinator.Phase, Is.EqualTo(RunPhase.Running));
            Assert.That(coordinator.Run, Is.SameAs(current));
            Assert.That(current.Clock.IsRunning, Is.True);
            Assert.That(builders.Count, Is.EqualTo(2));
            Assert.That(builders[0].DisposeCount, Is.EqualTo(1));
            Assert.That(builders[1].DisposeCount, Is.Zero);
            Assert.That(coordinator.LastError, Is.Empty,
                "A cancelled start's delayed recovery failure belongs to the old transition, not the current run.");
        }

        private static async Task CompleteWithinTimeout(Task task)
        {
            if (!task.IsCompleted)
                Assert.That(await Task.WhenAny(task, Task.Delay(5000)), Is.SameAs(task),
                    "A released scene operation must settle without waiting for another transition.");
            await task;
        }

        private sealed class QueuedLoader : ISceneLoader
        {
            public readonly Queue<Task> BattleLoads = new Queue<Task>();
            public readonly Queue<Task> MenuLoads = new Queue<Task>();
            public Task LoadBattleAsync() => BattleLoads.Dequeue();
            public Task LoadMainMenuAsync() => MenuLoads.Dequeue();
        }

        private sealed class SessionBuilder : IRunBuilder
        {
            public RunModel Run { get; } = new RunModel(Guid.NewGuid(), 7,
                new DefinitionCatalog(Enum.GetValues(typeof(UnitKind)).Cast<UnitKind>().Select(kind =>
                    new UnitDefinition(kind.ToString(), kind, 10, 2, 0.26, 1, BigInteger.One))),
                new ChunkGenerator(7, MapSettings.TestDefaults(0)));
            public int DisposeCount { get; private set; }
            public void BuildWorld() => Run.World.GetChunk(default);
            public void CreatePlayer() => Run.SetPlayer((PlayerModel)new PlayerFactory(Run.World.Units).Spawn(
                new UnitSpawnRequest(Run.Id, UnitKind.Player, default, Run.Definitions.GetUnit(UnitKind.Player))));
            public void CreateInitialEnemies() => new GroundEnemyFactory(Run.World.Units).Spawn(
                new UnitSpawnRequest(Run.Id, UnitKind.Normal, WorldPosition.FromLocal(new DVec2(5, 0)), Run.Definitions.GetUnit(UnitKind.Normal)));
            public void Dispose() { DisposeCount++; Run.Dispose(); }
        }
    }
}
