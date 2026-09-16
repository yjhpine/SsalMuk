using System;
using SsalMuk.Core;
using SsalMuk.Presentation;
using UnityEngine;

namespace SsalMuk.Unity
{
    public sealed class RunScope : IRunBuilder
    {
        private readonly GameCatalog catalog;
        private readonly InitialRunBuilder builder;
        private GameObject root;
        private WorldPresenter presenter;
        private MovementSystem movement;
        private BattleRunner runner;
        private bool disposed;
        public RunModel Run { get; }
        public RunScope(GameCatalog catalog)
        {
            this.catalog = catalog; catalog.ValidatePresentation();
            Guid id = Guid.NewGuid(); int seed = BitConverter.ToInt32(id.ToByteArray(), 0);
            var definitions = catalog.CreateDefinitions(); var streams = new SeedStreams(seed);
            Run = new RunModel(id, seed, definitions, new ChunkGenerator(streams.MapSeed, catalog.Defaults.CreateMapSettings()));
            builder = new InitialRunBuilder(Run, catalog.Defaults, RefreshViews);
        }
        public void BuildWorld() => builder.BuildWorld();
        public void CreatePlayer()
        {
            builder.CreatePlayer();
            var navigation = new NavigationService(Run.World);
            movement = new MovementSystem(Run.World, navigation, Run.Player, catalog.Defaults.CreateMovementSettings());
            runner = root.AddComponent<BattleRunner>(); runner.Configure(Run, movement, new LowAiController(Run.Player, Run.World, navigation), presenter, builder.PrepareTerrain);
        }
        public void CreateInitialEnemies() => builder.CreateInitialEnemies();
        private void RefreshViews()
        {
            if (root == null)
            {
                root = new GameObject("Run " + Run.Id.ToString("N")); var view = root.AddComponent<WorldView>();
                view.Initialize(catalog); presenter = new WorldPresenter(view);
            }
            presenter.Refresh(Run);
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            if (runner != null) runner.enabled = false;
            movement?.Dispose(); builder.Dispose();
            if (root != null) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
        }
    }
}
