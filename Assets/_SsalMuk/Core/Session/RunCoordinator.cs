using System;
using System.Threading.Tasks;

namespace SsalMuk.Core
{
    public sealed class RunCoordinator : IDisposable
    {
        private readonly ISceneLoader sceneLoader;
        private readonly Func<IRunBuilder> createBuilder;
        private IRunBuilder builder;
        private Task pending;
        private long generation;
        private bool returningToMenu;
        public RunPhase Phase { get; private set; } = RunPhase.MainMenu;
        public RunModel Run => builder?.Run;
        public RunResult Result { get; private set; }
        public Task RestartAsync()
        {
            if (Phase == RunPhase.Disposed) throw new ObjectDisposedException(nameof(RunCoordinator));
            return Phase == RunPhase.Results ? BeginStart() : pending ?? Task.CompletedTask;
        }
        public Task ReturnToMenuAsync()
        {
            if (Phase == RunPhase.Disposed) throw new ObjectDisposedException(nameof(RunCoordinator));
            if (Phase == RunPhase.MainMenu || returningToMenu) return pending ?? Task.CompletedTask;
            long attempt = checked(++generation); returningToMenu = true; LastError = "";
            Cleanup(); SetPhase(RunPhase.Loading); return pending = ReturnCoreAsync(attempt);
        }
        private async Task ReturnCoreAsync(long attempt)
        {
            try { await sceneLoader.LoadMainMenuAsync(); }
            catch (Exception error) { if (attempt == generation) LastError = error.Message; }
            if (attempt != generation || Phase == RunPhase.Disposed) return;
            returningToMenu = false; SetPhase(RunPhase.MainMenu);
        }
        public string LastError { get; private set; } = "";
        public event Action<RunPhase> PhaseChanged;
        public RunCoordinator(ISceneLoader sceneLoader, Func<IRunBuilder> createBuilder)
        { this.sceneLoader = sceneLoader ?? throw new ArgumentNullException(nameof(sceneLoader)); this.createBuilder = createBuilder ?? throw new ArgumentNullException(nameof(createBuilder)); }
        public Task StartRunAsync()
        {
            if (Phase == RunPhase.Disposed) throw new ObjectDisposedException(nameof(RunCoordinator));
            if (Phase != RunPhase.MainMenu) return pending ?? Task.CompletedTask;
            return BeginStart();
        }
        private Task BeginStart()
        {
            LastError = ""; long attempt = checked(++generation); SetPhase(RunPhase.Loading);
            return pending = StartCoreAsync(attempt);
        }
        private async Task StartCoreAsync(long attempt)
        {
            try
            {
                builder = createBuilder() ?? throw new InvalidOperationException("Run builder is missing.");
                builder.Run.Clock.Stop(); builder.Run.Phase = RunPhase.Loading; builder.Run.Completed += OnCompleted;
                await sceneLoader.LoadBattleAsync();
                if (attempt != generation || Phase == RunPhase.Disposed) return;
                SetPhase(RunPhase.BuildingWorld); builder.BuildWorld();
                SetPhase(RunPhase.CreatingPlayer); builder.CreatePlayer();
                SetPhase(RunPhase.CreatingEnemies); builder.CreateInitialEnemies();
                Result = null; builder.Run.Clock.Start(); SetPhase(RunPhase.Running);
            }
            catch (Exception error)
            {
                if (attempt != generation || Phase == RunPhase.Disposed) return;
                LastError = error.Message; Cleanup();
                try { await sceneLoader.LoadMainMenuAsync(); }
                catch (Exception menuError) { LastError += " / " + menuError.Message; }
                if (attempt == generation && Phase != RunPhase.Disposed) SetPhase(RunPhase.MainMenu);
            }
        }
        private void OnCompleted(RunResult result)
        {
            if (Phase != RunPhase.Running || builder == null || result.RunId != builder.Run.Id) return;
            Result = result; Cleanup(); SetPhase(RunPhase.Results);
        }
        private void SetPhase(RunPhase phase)
        {
            Phase = phase; if (builder != null) builder.Run.Phase = phase; PhaseChanged?.Invoke(phase);
        }
        private void Cleanup()
        {
            if (builder == null) return;
            var old = builder; builder = null; old.Run.Completed -= OnCompleted; old.Run.Clock.Stop(); old.Dispose();
        }
        public void Dispose()
        {
            if (Phase == RunPhase.Disposed) return;
            generation = checked(generation + 1); Cleanup(); SetPhase(RunPhase.Disposed); PhaseChanged = null;
        }
    }
}
