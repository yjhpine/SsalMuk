using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BigInteger = System.Numerics.BigInteger;
using SsalMuk.Core;
using SsalMuk.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using Stopwatch = System.Diagnostics.Stopwatch;
namespace SsalMuk.Unity.Diagnostics
{
    public sealed class ScenarioRunner : MonoBehaviour
    {
        private readonly List<string> observations = new List<string>();
        private readonly List<RuntimeSnapshot> samples = new List<RuntimeSnapshot>();
        private MetricsCollector metrics;
        private DiagnosticSession session;
        private GameObject diagnosticCamera;
        private string directory;
        private int errors;
        private bool previousBackground, backgroundOverridden;
        public ScenarioReport Report { get; private set; }
        public bool Finished { get; private set; }

        public IEnumerator Run(string scenarioId, string outputDirectory, string requestRunId = null, string sourceHash = null)
        {
            if (Report != null) throw new InvalidOperationException("A scenario runner executes once.");
            directory = Path.GetFullPath(outputDirectory); Directory.CreateDirectory(directory);
            Report = new ScenarioReport { runId = requestRunId ?? Guid.NewGuid().ToString("N"), sourceHash = sourceHash ?? "",
                scenario = scenarioId, status = "Running", startedUtc = DateTime.UtcNow.ToString("O"), seed = 260917,
                unityVersion = Application.unityVersion, environment = Application.isEditor ? "Editor" : "WindowsPlayer",
                processor = SystemInfo.processorType, graphics = SystemInfo.graphicsDeviceName, operatingSystem = SystemInfo.operatingSystem,
                systemMemoryMB = SystemInfo.systemMemorySize, width = Screen.width, height = Screen.height,
                executionMode = "All 0.02-second simulation ticks; batched per rendered frame. Frame wall time is not realtime game FPS.",
                preset = "Catalog defaults; explicit scenario fixtures recorded in observations." };
            metrics = new MetricsCollector(); Application.logMessageReceived += OnLog;
            previousBackground = Application.runInBackground; backgroundOverridden = true; Application.runInBackground = true;
            var stack = new Stack<IEnumerator>(); stack.Push(Dispatch(scenarioId));
            Exception failure = null;
            while (stack.Count > 0)
            {
                object current = null; bool moved = false;
                try
                {
                    moved = stack.Peek().MoveNext();
                    if (moved) current = stack.Peek().Current;
                    else { (stack.Pop() as IDisposable)?.Dispose(); }
                }
                catch (Exception error) { failure = error; break; }
                if (!moved) continue;
                if (current is IEnumerator nested) stack.Push(nested);
                else { yield return current; metrics.Frame(Time.unscaledDeltaTime); }
            }
            foreach (var iterator in stack) (iterator as IDisposable)?.Dispose();
            session?.Dispose(); session = null;
            if (diagnosticCamera != null) Destroy(diagnosticCamera);
            if (AppRoot.Instance != null) Destroy(AppRoot.Instance.gameObject);
            yield return null; yield return null;
            if (failure == null && (errors > 0 || observations.Count == 0 || samples.Count == 0))
                failure = new InvalidOperationException("Scenario did not finish with complete error-free observations.");
            Application.logMessageReceived -= OnLog;
            Report.errorCount = errors; Report.observations = observations.ToArray(); Report.samples = samples.ToArray();
            Report.metrics = metrics.Summary(); metrics.Dispose(); Report.steps = Report.metrics.tickCount;
            Report.status = failure == null ? "Passed" : "Failed"; Report.failureKind = failure == null ? "" : "ScenarioFailed";
            Report.message = failure?.ToString() ?? ""; Report.completedUtc = DateTime.UtcNow.ToString("O");
            Write("scenario.json", Report); Finished = true; RestoreBackground();
        }
        private IEnumerator Dispatch(string id)
        {
            var definition = ScenarioDefinition.Get(id);
            if (id == "EndToEnd") yield return EndToEnd(1);
            else if (id == "RepeatedRestart") yield return EndToEnd(3);
            else
            {
                if (AppRoot.Instance != null) Destroy(AppRoot.Instance.gameObject);
                yield return null; yield return null;
                diagnosticCamera = new GameObject("Diagnostic camera", typeof(Camera));
                diagnosticCamera.transform.position = new Vector3(0, 0, -10);
                var camera = diagnosticCamera.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 10;
                camera.backgroundColor = new Color(.08f, .14f, .16f); camera.clearFlags = CameraClearFlags.SolidColor;
                if (id.StartsWith("PersistentWorld", StringComparison.Ordinal)) yield return Persistence(definition.Seconds);
                else if (id == "CrowdCorridor") yield return CrowdCorridor();
                else yield return HighGrowth();
            }
        }
        private void OnLog(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        private void Require(bool condition, string detail)
        { if (!condition) throw new InvalidOperationException(detail); }
        private void Observe(string name) { if (!observations.Contains(name)) observations.Add(name); }
        private void Tick(RunModel run, RunSimulation simulation)
        {
            Require(run.Phase == RunPhase.Running, "The scenario run stopped before its requested duration.");
            simulation.SetViewBounds(new WorldRect(run.Player.Position, 16, 9));
            metrics.Tick(() => simulation.Step(run.Clock.FixedStep));
            Report.simulationSeconds += run.Clock.FixedStep;
        }
        private void Sample(RunModel run, RunSimulation simulation, WorldView view)
        { samples.Add(RuntimeProbe.Capture(run, simulation, view)); metrics.Memory(); Write("progress.json", new ScenarioProgress { scenario = Report.scenario, elapsed = run.Clock.ElapsedSeconds, units = run.Units.Count, experience = run.Experience.Count, steps = Report.steps = metrics.TickCount }); }
        private static BigInteger TotalExperience(RunModel run) => run.GrowthSettings.CostForLevels(1, run.Player.Level - 1) + run.Player.Growth.ExperienceIntoLevel;
        private IEnumerator WaitFor(Func<bool> condition, string description)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 20;
            while (!condition() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Require(condition(), "Timed out: " + description);
        }
        private void Render(RunModel run, WorldView view)
        { metrics.Render(() => new WorldPresenter(view).Refresh(run)); }
        private IEnumerator Screenshot(string name)
        {
            yield return null; yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                if (!HasRenderedContent(texture) && !Application.isEditor)
                {
                    Destroy(texture); texture = CaptureOffscreen(); Observe("OffscreenCameraCapture");
                }
                Require(HasRenderedContent(texture), "Screenshot has no rendered game content: " + name);
                File.WriteAllBytes(Path.Combine(directory, name + ".png"), texture.EncodeToPNG());
            }
            finally { Destroy(texture); }
        }
        private static bool HasRenderedContent(Texture2D texture)
        {
            var pixels = texture.GetPixels32(); var colors = new HashSet<int>();
            for (int i = 0; i < pixels.Length && colors.Count < 8; i += Math.Max(1, pixels.Length / 12000))
            {
                var p = pixels[i]; colors.Add((p.r >> 3) << 10 | (p.g >> 3) << 5 | p.b >> 3);
            }
            return colors.Count >= 8;
        }
        private Texture2D CaptureOffscreen()
        {
            var camera = Camera.main ?? diagnosticCamera?.GetComponent<Camera>();
            Require(camera != null, "No runtime camera for offscreen capture.");
            var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
            var overlays = FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay)
                .Select(c => (canvas: c, camera: c.worldCamera, distance: c.planeDistance)).ToArray();
            var target = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32); target.Create();
            Texture2D result = null;
            try
            {
                // A hidden Windows swap chain need not present. Render the same camera and active UI
                // to an explicit target without opening a window or changing any game model.
                foreach (var overlay in overlays)
                { overlay.canvas.renderMode = RenderMode.ScreenSpaceCamera; overlay.canvas.worldCamera = camera; overlay.canvas.planeDistance = camera.nearClipPlane + 1; }
                camera.targetTexture = target; Canvas.ForceUpdateCanvases();
                if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null) camera.Render();
                else
                {
                    var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target };
                    Require(UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(camera, request), "Render pipeline cannot capture the runtime camera.");
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, request);
                }
                RenderTexture.active = target;
                result = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                result.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); result.Apply(); return result;
            }
            catch { if (result != null) Destroy(result); throw; }
            finally
            {
                camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
                foreach (var overlay in overlays)
                { overlay.canvas.renderMode = RenderMode.ScreenSpaceOverlay; overlay.canvas.worldCamera = overlay.camera; overlay.canvas.planeDistance = overlay.distance; }
                Canvas.ForceUpdateCanvases(); target.Release(); Destroy(target);
            }
        }
        private UnitModel Spawn(RunModel run, UnitKind kind, WorldPosition at, double health = 1000000000, double? damage = null, DVec2? direction = null)
        {
            var original = run.Definitions.GetUnit(kind);
            var definition = new UnitDefinition("scenario-" + kind, kind, health, original.MoveSpeed, original.BodyRadius, damage ?? original.ContactDamage, original.ExperienceReward, original.HurtRadius, original.VisualFootOffset);
            UnitFactory factory = kind == UnitKind.Air ? (UnitFactory)new AirEnemyFactory(run.World.Units) : new GroundEnemyFactory(run.World.Units);
            return factory.Spawn(new UnitSpawnRequest(run.Id, kind, at, definition, direction));
        }
        private WorldPosition FreeNear(RunModel run, WorldPosition at, double radius)
        {
            if (run.World.Query.IsCircleFree(at, radius)) return at;
            for (int ring = 1; ring < 30; ring++) for (int i = 0; i < 16; i++)
            {
                var candidate = at.Offset(new DVec2(Math.Cos(i * Math.PI / 8), Math.Sin(i * Math.PI / 8)) * (ring * .5));
                if (run.World.Query.IsCircleFree(candidate, radius)) return candidate;
            }
            throw new InvalidOperationException("No safe fixture position.");
        }
        private IEnumerator EndToEnd(int cycles)
        {
            yield return SceneManager.LoadSceneAsync(AppRoot.MainMenuScenePath); yield return null;
            var app = AppRoot.Instance; Require(app != null && app.Coordinator.Phase == RunPhase.MainMenu, "MainMenu did not bootstrap.");
            app.Menu.StartButton.onClick.Invoke(); yield return WaitFor(() => app.Coordinator.Phase == RunPhase.Running, "GameStart");
            Observe("GameStart");
            var identities = new HashSet<Guid>();
            for (int cycle = 0; cycle < cycles; cycle++)
            {
                var run = app.Coordinator.Run; Require(identities.Add(run.Id), "Restart reused a run identity.");
                Report.seed = run.Seed;
                var runner = FindAnyObjectByType<BattleRunner>(); runner.enabled = false;
                var simulation = runner.Simulation; var view = FindAnyObjectByType<WorldView>();
                Require(run.Player.Weapons.Kinds.SequenceEqual(new[] { WeaponKind.Sword }), "A new run did not start with only Sword.");
                Require(RunScope.ActiveCount == 1, "More than one production run scope.");
                Sample(run, simulation, view);
                long orb = run.World.AddExperience(run.Player.Position.Offset(new DVec2(2, 0)), 5, run.Clock.ElapsedSeconds);
                var before = TotalExperience(run); Observe("ExperienceGrounded");
                bool attracted = false, flew = false;
                WorldPosition prior = default;
                for (int tick = 0; tick < 250 && run.World.TryGetExperience(orb, out var record); tick++)
                {
                    prior = record.Position; Tick(run, simulation);
                    if (run.World.TryGetExperience(orb, out record))
                    {
                        if (record.State == ExperienceState.Attracting) { attracted = true; Observe("ExperienceAttracting"); }
                        if (record.State == ExperienceState.Attracting && record.Position.DistanceTo(prior) > 1e-6) { flew = true; Observe("ExperienceFlight"); }
                        Require(TotalExperience(run) == before, "Experience was paid before body contact.");
                    }
                    Render(run, view); yield return null;
                }
                Require(attracted && flew && !run.World.TryGetExperience(orb, out _), "Experience did not complete its real flight.");
                Require(TotalExperience(run) >= before + 5, "Body contact did not pay experience.");
                Observe("ExperienceContact");
                Require(simulation.Sword.LaunchCount > 0, "Automatic sword did not launch."); Observe("Attack");
                yield return null;
                Require(app.LevelUp.IsVisible && app.LevelUp.Buttons.Count == 3, "Reward choice UI is missing.");
                var offer = run.CurrentOffer; var pending = run.Player.Growth.PendingChoices;
                app.LevelUp.Buttons[0].onClick.Invoke(); Tick(run, simulation); yield return null;
                Require(run.Player.Growth.PendingChoices == pending - 1, "Choice button did not consume exactly one opportunity.");
                Require(!run.Commands.TryQueueChoice(run.Id, offer.Id, 0), "Old offer was accepted."); Observe("ChoiceButton");
                Render(run, view); Sample(run, simulation, view);
                if (cycle == 0) yield return Screenshot("reward-selected");

                // Leave actual in-flight XP, projectiles and leases for the lifecycle check.
                var growth = new GrowthService(run);
                if (!run.Player.Weapons.Owns(WeaponKind.Fireball)) growth.Equip(WeaponKind.Fireball);
                Spawn(run, UnitKind.Air, run.Player.Position.Offset(new DVec2(5, 0)), direction: new DVec2(0, 1));
                long flyingId = run.World.AddExperience(run.Player.Position.Offset(new DVec2(1.2, 0)), 1, run.Clock.ElapsedSeconds);
                Tick(run, simulation); Render(run, view);
                Require(run.World.TryGetExperience(flyingId, out var flying) && flying.State == ExperienceState.Attracting, "Restart fixture needs flying XP.");
                var oldXpView = view.ExperienceViews.Single(x => x.Lease.EntityId == flyingId); var oldLease = oldXpView.Lease;
                Sample(run, simulation, view);
                double deadline = run.Clock.ElapsedSeconds + 3;
                while (app.Coordinator.Phase == RunPhase.Running && run.Clock.ElapsedSeconds < deadline)
                {
                    if (run.Clock.ElapsedSeconds >= run.Player.InvulnerableUntil)
                        Spawn(run, UnitKind.Air, run.Player.Position, damage: 1000000000);
                    Tick(run, simulation); yield return null;
                }
                Require(app.Coordinator.Phase == RunPhase.Results && !run.Player.IsAlive, "Normal contact damage did not finish the run.");
                Observe("Death"); Require(app.Coordinator.Result.RunId == run.Id && app.Results.RestartButton.interactable, "Results did not preserve the dead run.");
                Observe("Results");
                yield return null; yield return null;
                Require(run.World.Units.Count == 0 && run.Experience.Count == 0 && simulation.Projectiles.Count == 0 &&
                    simulation.Weapons.Values.All(x => x.ActiveAttacks.Count == 0 && x.PendingStrikes.IsZero) && simulation.Spawns.Pending.Count == 0, "Old run retained model work.");
                Require(RunScope.ActiveCount == 0 && FindAnyObjectByType<WorldView>() == null, "Old scope or views survived death.");
                Require(oldXpView == null || !oldXpView.TrySetPosition(oldLease, Vector3.one), "Old XP callback survived disposal.");
                Require(!new ProgressionService(run).AddExperience(1), "Disposed run accepted a late XP award.");
                Observe("CleanScope");
                if (cycle == 0) yield return Screenshot("results");
                app.Results.RestartButton.onClick.Invoke(); app.Results.RestartButton.onClick.Invoke();
                yield return WaitFor(() => app.Coordinator.Phase == RunPhase.Running, "Restart");
                Observe("Restart");
                var nextRunner = FindAnyObjectByType<BattleRunner>(); nextRunner.enabled = false;
                Require(app.Coordinator.Run.Id != run.Id && app.Coordinator.Run.Player.Level == 1, "Restart retained growth.");
                Require(app.Coordinator.Run.Experience.Count == 0 && RunScope.ActiveCount == 1, "New run is not clean.");
                Sample(app.Coordinator.Run, nextRunner.Simulation, FindAnyObjectByType<WorldView>());
            }
            Observe("RestartCycles=" + cycles);
        }

        private IEnumerator Persistence(double seconds)
        {
            var catalog = Resources.Load<GameCatalog>("Bootstrap/GameCatalog");
            session = new DiagnosticSession(catalog, Report.seed);
            var run = session.Run; var simulation = session.Simulation; var view = session.View;
            Report.preset = "Seed 260917; generated map and default continuous spawn; player/boss HP 1e9; all 4 weapons: Damage +1000, Range +20, Speed +10. Every 0.02s tick executed; ground/boss/XP retained, outgoing air waves depart beyond the screen margin. Separate sentinels and 300-value XP ledger.";
            var growth = new GrowthService(run);
            foreach (WeaponKind kind in Enum.GetValues(typeof(WeaponKind)))
            {
                if (!run.Player.Weapons.Owns(kind)) growth.Equip(kind);
                growth.Upgrade(kind, UpgradeKind.Damage, 1000); growth.Upgrade(kind, UpgradeKind.Range, 20); growth.Upgrade(kind, UpgradeKind.Speed, 10);
            }
            var normal = Spawn(run, UnitKind.Normal, FreeNear(run, run.Player.Position.Offset(new DVec2(96, 0)), .26));
            var air = Spawn(run, UnitKind.Air, run.Player.Position.Offset(new DVec2(64, 16)), direction: new DVec2(1, 0));
            var originalGround = normal.Position;
            var far = new WorldPosition(new ChunkCoord(100, 100), new DVec2(16.5, 16.5));
            var waiting = new Dictionary<long, WorldPosition>();
            for (int i = 0; i < 12; i++) { var at = far.Offset(new DVec2(i % 4, i / 4)); waiting.Add(run.World.AddExperience(at, 25, 0), at); }
            long registered = run.Units.Count, removed = 0, departedAir = 0; bool aliveRemoved = false;
            Action<UnitModel> onRegister = _ => registered++;
            Action<UnitModel> onRemove = unit => { removed++; if (unit.IsAlive) { if (unit.Kind == UnitKind.Air) departedAir++; else aliveRemoved = true; } };
            run.World.Units.Registered += onRegister; run.World.Units.Removed += onRemove;
            try
            {
                Sample(run, simulation, view);
                long flyingId = run.World.AddExperience(run.Player.Position.Offset(new DVec2(1.2, 0)), 1, 0);
                Tick(run, simulation);
                Require(run.World.TryGetExperience(flyingId, out var flying) && flying.State == ExperienceState.Attracting, "Boundary fixture did not attract.");
                var from = flying.Position;
                var nextChunk = new ChunkCoord(run.Player.Position.Chunk.X + 1, run.Player.Position.Chunk.Y);
                run.World.MoveUnit(run.Player.Id, new WorldPosition(nextChunk, new DVec2(16.5, 16.5)));
                for (int i = 0; i < 10; i++) Tick(run, simulation);
                metrics.Render(session.Render);
                Require(run.World.TryGetExperience(flyingId, out flying) && flying.Position != from && flying.State == ExperienceState.Attracting, "Attracted XP froze or vanished outside the view.");
                Require(!view.ExperienceViews.Any(v => v.Lease.EntityId == flyingId), "Boundary fixture did not leave the view.");
                Observe("ExperienceBoundaryFlight"); Sample(run, simulation, view);
                double nextSample = 60;
                while (run.Clock.ElapsedSeconds + 1e-8 < seconds)
                {
                    long start = Stopwatch.GetTimestamp(); int batch = 0;
                    do { Tick(run, simulation); batch++; }
                    while (run.Clock.ElapsedSeconds + 1e-8 < seconds && batch < 50 &&
                        (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency < 8);
                    metrics.Render(session.Render);
                    if (run.Clock.ElapsedSeconds >= nextSample || run.Clock.ElapsedSeconds + 1e-8 >= seconds)
                    {
                        Require(!aliveRemoved && departedAir == simulation.DepartedAirCount && registered - removed == run.Units.Count, "A monster was discarded outside the air-departure rule.");
                        Require(simulation.Weapons.Values.All(w => w.ActiveAttacks.All(a => a.StartedAt + a.ActiveSeconds > run.Clock.ElapsedSeconds)),
                            "Expired attack instances accumulated during persistent simulation.");
                        foreach (var pair in waiting)
                            Require(run.World.TryGetExperience(pair.Key, out var orb) && orb.Value == 25 && orb.Position == pair.Value && orb.State == ExperienceState.Grounded, "Far waiting XP changed.");
                        Require(run.World.Units.TryGet(normal.Id, out _), "Far ground sentinel was deleted.");
                        Sample(run, simulation, view); nextSample += 60;
                    }
                    yield return null;
                }
                Require(normal.Position.DistanceTo(originalGround) > 1, "Far ground enemy never progressed.");
                Require(air.IsAlive && !run.World.Units.TryGet(air.Id, out _) && simulation.DepartedAirCount > 0, "Outgoing air enemy was retained or counted as dead.");
                Require(!run.World.TryGetExperience(flyingId, out _), "Offscreen attracted XP never completed its flight.");
                Require(simulation.Spawns.SpawnedCount(UnitKind.Boss) == (long)Math.Floor(seconds / 300), "Boss schedule did not reach every deadline.");
                Require(run.Units.Count(x => x.Kind == UnitKind.Boss) == simulation.Spawns.SpawnedCount(UnitKind.Boss), "Overlapping diagnostic bosses were lost.");
                Observe("AllFixedTicks"); Observe("GroundAndExperienceRetention"); Observe("FarGroundProgress"); Observe("AirWaveDeparture"); Observe("FarExperiencePreserved"); Observe("OverlappingBosses");
                var originalPlayer = run.Player.Position;
                run.World.MoveUnit(run.Player.Id, far); metrics.Render(session.Render);
                Require(waiting.Keys.All(id => view.ExperienceViews.Any(v => v.Lease.EntityId == id)), "Returning did not restore far XP views.");
                Sample(run, simulation, view); yield return Screenshot("far-experience-return");
                run.World.MoveUnit(run.Player.Id, originalPlayer); metrics.Render(session.Render); Observe("ViewRoundTrip");
                // A separately recorded visual-load sample, kept out of the 10/30 minute spawn accounting.
                for (int i = 0; i < 600; i++)
                {
                    double angle = i * Math.PI * 2 / 600;
                    run.World.AddExperience(run.Player.Position.Offset(new DVec2(Math.Cos(angle), Math.Sin(angle)) * (5 + i % 10 * .6)), i % 3 == 0 ? 1 : i % 3 == 1 ? 5 : 25, run.Clock.ElapsedSeconds);
                }
                for (int frame = 0; frame < 60; frame++) { metrics.Render(session.Render); yield return null; }
                Require(view.VisibleExperienceCount >= 600, "Glowing XP view load is missing.");
                Sample(run, simulation, view); Observe("Glow600Views"); yield return Screenshot("glow-load");
            }
            finally { run.World.Units.Registered -= onRegister; run.World.Units.Removed -= onRemove; }
        }
        private IEnumerator CrowdCorridor()
        {
            session = new DiagnosticSession(Resources.Load<GameCatalog>("Bootstrap/GameCatalog"), Report.seed, false, new CorridorTerrain());
            var run = session.Run; var simulation = session.Simulation;
            Report.preset = "72 normal monsters, HP 1e9; actual AI/combat/crowd solver; fixed wall with a 4-cell opening; player HP 1e9.";
            run.World.MoveUnit(run.Player.Id, WorldPosition.FromLocal(new DVec2(24.5, 16.5)));
            var origins = new Dictionary<long, WorldPosition>();
            for (int i = 0; i < 72; i++)
            {
                var unit = Spawn(run, UnitKind.Normal, WorldPosition.FromLocal(new DVec2(7.5 + i % 8 * .7, 13.5 + i / 8 * .7)));
                origins.Add(unit.Id, unit.Position);
            }
            double maximumStep = 0;
            for (int i = 0; i < 600; i++)
            {
                Tick(run, simulation); maximumStep = Math.Max(maximumStep, session.Movement.MaximumDisplacement);
                foreach (var unit in run.Units) Require(run.World.Query.IsCircleFree(unit.Position, unit.BodyRadius), "Crowd crossed a wall.");
                if (i % 5 == 0) { metrics.Render(session.Render); yield return null; }
                if (i % 100 == 0) Sample(run, simulation, session.View);
            }
            int moved = origins.Count(x => run.World.Units.Get(x.Key).Position.DistanceTo(x.Value) > .5);
            Write("crowd-detail.json", new CrowdDetail { moved = moved, units = run.Units.Count, maximumStep = maximumStep,
                distances = origins.Select(x => run.World.Units.Get(x.Key).Position.DistanceTo(x.Value)).ToArray(),
                positions = origins.Select(x => run.World.Units.Get(x.Key).Position.Local.X + "," + run.World.Units.Get(x.Key).Position.Local.Y).ToArray() });
            Require(run.Units.Count == 73 && moved >= 60, "Crowd did not keep moving: " + moved + "/72 moved, units=" + run.Units.Count);
            Require(maximumStep < .25, "Crowd correction teleported a body.");
            Observe("Crowd72"); Observe("BodySafeCorridor"); Observe("NoTeleport"); Observe("AllFixedTicks");
            Sample(run, simulation, session.View); yield return Screenshot("crowd-corridor");
        }
        private IEnumerator HighGrowth()
        {
            var catalog = Resources.Load<GameCatalog>("Bootstrap/GameCatalog");
            Report.preset = "Calculator: BigInteger 10^400 XP/copy/repeat levels; explicit numeric precision failure. Runtime tiers: copy upgrades 0/2/8, repeat upgrades 0/2/8, speed +2, 4 weapons, 5s each.";
            session = new DiagnosticSession(catalog, Report.seed, false);
            var run = session.Run; var growth = new GrowthService(run); var huge = BigInteger.Pow(10, 400);
            Require(new ProgressionService(run).AddExperience(huge), "Huge progression was rejected.");
            Require(TotalExperience(run) == huge, "Huge experience lost precision.");
            growth.Upgrade(WeaponKind.Sword, UpgradeKind.Copies, huge); growth.Upgrade(WeaponKind.Sword, UpgradeKind.Repeats, huge);
            var calculated = StatCalculator.Calculate(run.Definitions.GetWeapon(WeaponKind.Sword), run.Player.Weapons.Get(WeaponKind.Sword), run.GrowthSettings);
            Require(calculated.Copies == 1 + 2 * huge && calculated.Repeats == 1 + huge, "Huge integer growth was capped.");
            bool rejected = false;
            try { growth.Upgrade(WeaponKind.Sword, UpgradeKind.Damage, huge); } catch (NumericRangeException) { rejected = true; }
            Require(rejected && run.Player.Weapons.Get(WeaponKind.Sword).GetLevel(UpgradeKind.Damage).IsZero, "Unrepresentable damage was silently accepted or partially applied.");
            Observe("BigIntegerExact"); Observe("NumericLimitExplicit");
            session.Dispose(); session = null; yield return null; yield return null;
            foreach (int tier in new[] { 0, 2, 8 })
            {
                session = new DiagnosticSession(catalog, Report.seed + tier, false); run = session.Run; growth = new GrowthService(run);
                Spawn(run, UnitKind.Air, run.Player.Position.Offset(new DVec2(4, 0)), health: 1000000000000, direction: new DVec2(1, 0));
                foreach (WeaponKind kind in Enum.GetValues(typeof(WeaponKind)))
                {
                    if (!run.Player.Weapons.Owns(kind)) growth.Equip(kind);
                    growth.Upgrade(kind, UpgradeKind.Speed, 2);
                    if (tier > 0) { growth.Upgrade(kind, UpgradeKind.Copies, tier); growth.Upgrade(kind, UpgradeKind.Repeats, tier); }
                }
                for (int i = 0; i < 250; i++)
                { Tick(run, session.Simulation); if (i % 5 == 0) { metrics.Render(session.Render); yield return null; } }
                foreach (var weapon in session.Simulation.Weapons.Values)
                {
                    var stats = StatCalculator.Calculate(run.Definitions.GetWeapon(weapon.Kind), run.Player.Weapons.Get(weapon.Kind), run.GrowthSettings);
                    long strikes = 0; double until = run.Clock.ElapsedSeconds;
                    for (long group = 0; group * stats.PeriodSeconds <= until + 1e-8; group++)
                        for (int repeat = 0; repeat <= tier; repeat++)
                            if (group * stats.PeriodSeconds + (tier == 0 ? 0 : stats.PeriodSeconds * .6 * repeat / tier) <= until + 1e-8) strikes++;
                    Require(weapon.LaunchCount == strikes * (1 + 2 * tier), "A copy/repeat was omitted at tier " + tier + ": " + weapon.Kind);
                    Require(weapon.MaximumDispatchDelay <= .020001, "Attack reservation exceeded one fixed tick.");
                }
                Sample(run, session.Simulation, session.View); Observe("RuntimeTier=" + tier);
                yield return Screenshot("growth-tier-" + tier);
                session.Dispose(); session = null; yield return null; yield return null;
            }
            Observe("NoAttackOmission");
        }
        private sealed class CorridorTerrain : IChunkGenerator
        {
            public ChunkData Generate(ChunkCoord coord)
            {
                var blocked = new bool[1024];
                if (coord == default(ChunkCoord))
                    for (int y = 4; y < 29; y++) if (y < 14 || y >= 18) blocked[y * 32 + 16] = true;
                return new ChunkData(coord, blocked, 1);
            }
        }
        private void Write(string name, object payload)
        {
            string path = Path.Combine(directory, name), temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(payload, true));
            for (int attempt = 0; ; attempt++)
            {
                try { if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path); return; }
                catch (IOException) when (attempt < 4 && File.Exists(temp))
                { System.Threading.Thread.Sleep(20 * (attempt + 1)); }
            }
        }
        private void OnDestroy()
        { Application.logMessageReceived -= OnLog; metrics?.Dispose(); session?.Dispose(); RestoreBackground(); if (diagnosticCamera != null) Destroy(diagnosticCamera); }
        private void RestoreBackground()
        { if (backgroundOverridden) { Application.runInBackground = previousBackground; backgroundOverridden = false; } }
        [Serializable]
        private sealed class ScenarioProgress { public string scenario; public double elapsed; public int units, experience; public long steps; }
        [Serializable]
        private sealed class CrowdDetail { public int moved, units; public double maximumStep; public double[] distances; public string[] positions; }
    }
}
