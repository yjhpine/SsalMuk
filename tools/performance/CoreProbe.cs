using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using SsalMuk.Core;
using SsalMuk.Presentation;
using SsalMuk.Tests;

internal static class CoreProbe
{
    private static int repetitions, warmup, batches;
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };
    private sealed record TestResult(string name, bool passed, string error);
    private sealed record Batch(int operations, long elapsedTicks, double millisecondsPerOperation, long allocatedBytes, double bytesPerOperation);

    public static int Main(string[] args)
    {
        if (args.Length != 2) { Console.Error.WriteLine("Expected manifest and report paths."); return 2; }
        using var manifest = JsonDocument.Parse(File.ReadAllText(args[0]));
        var input = manifest.RootElement;
        repetitions = input.GetProperty("repetitions").GetInt32();
        warmup = input.GetProperty("warmup").GetInt32();
        batches = input.GetProperty("batches").GetInt32();
        var failures = new List<string>();
        var measurements = new List<object>();
        var results = new List<TestResult>();
        long calibrationBytes = 0;
        bool runnerSelfCheckPassed = false;
        try
        {
            runnerSelfCheckPassed = CheckRunner();
            if (!runnerSelfCheckPassed) throw new InvalidOperationException("Reflection runner self-check failed.");
            foreach (var fixture in input.GetProperty("fixtureNames").EnumerateArray())
                results.AddRange(RunFixture(Assembly.GetExecutingAssembly().GetType(fixture.GetString(), true)));
            if (results.Count == 0) throw new InvalidOperationException("No tests discovered.");
            long before = GC.GetAllocatedBytesForCurrentThread();
            var calibration = new byte[4096];
            calibrationBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            GC.KeepAlive(calibration);
            if (calibrationBytes < 4096) throw new InvalidOperationException("Allocation counter failed its positive calibration.");
            foreach (int count in new[] { 0, 300, 500 })
                Capture("collector-xp-" + count, () => Collector(count), measurements, failures);
            foreach (int count in new[] { 300, 500 })
                Capture("presenter-units-" + count, () => Presenter(count), measurements, failures);
        }
        catch (Exception error) { failures.Add(error.ToString()); }
        var report = new {
            schemaVersion = 1, runId = input.GetProperty("runId").GetString(), label = input.GetProperty("label").GetString(),
            sourceHash = input.GetProperty("sourceHash").GetString(), gitHead = input.GetProperty("gitHead").GetString(),
            utc = DateTime.UtcNow.ToString("O"), runtime = RuntimeInformation.FrameworkDescription,
            os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            processorCount = Environment.ProcessorCount, stopwatchFrequency = Stopwatch.Frequency,
            serverGc = System.Runtime.GCSettings.IsServerGC, tieredCompilation = false,
            allocationCounter = "GC.GetAllocatedBytesForCurrentThread", calibrationBytes, runnerSelfCheckPassed,
            warmup, repetitions, batches,
            limitations = "Standalone .NET 10 model/presenter diagnostic, test definitions, no Unity, renderer, GPU, live gameplay, or FPS measurement.",
            tests = new { total = results.Count, passed = results.Count(t => t.passed), failed = results.Count(t => !t.passed), results },
            measurements, failures
        };
        File.WriteAllText(args[1], JsonSerializer.Serialize(report, JsonOptions));
        return failures.Count == 0 && results.Count > 0 && results.All(t => t.passed) ? 0 : 1;
    }

    private static IEnumerable<TestResult> RunFixture(Type fixture)
    {
        var methods = fixture.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        string setupError = methods.SelectMany(m => m.GetCustomAttributes(true)).Any(a =>
            a is SetUpAttribute || a is TearDownAttribute || a is OneTimeSetUpAttribute || a is OneTimeTearDownAttribute)
            ? "SetUp/TearDown lifecycle is unsupported; fixture was not executed." : null;
        bool discovered = false;
        foreach (var method in methods.OrderBy(m => m.Name, StringComparer.Ordinal))
        {
            var cases = method.GetCustomAttributes<TestCaseAttribute>(true).ToArray();
            bool hasTest = method.IsDefined(typeof(TestAttribute), true);
            var allAttributes = fixture.GetCustomAttributes(true).Concat(method.GetCustomAttributes(true));
            string unsupported = allAttributes.Select(a => a.GetType()).Where(t => t.Namespace == "NUnit.Framework")
                .Where(t => t != typeof(TestAttribute) && t != typeof(TestCaseAttribute) && t != typeof(TestFixtureAttribute)
                    && t != typeof(CategoryAttribute) && t != typeof(DescriptionAttribute))
                .Select(t => t.Name).FirstOrDefault();
            if (!hasTest && cases.Length == 0)
            {
                if (method.GetCustomAttributes(true).Any(a => a.GetType().Name is "TestCaseSourceAttribute" or "TheoryAttribute"))
                { discovered = true; yield return new TestResult(fixture.FullName + "." + method.Name, false, "Unsupported test discovery attribute."); }
                continue;
            }
            if (cases.Length == 0) cases = new TestCaseAttribute[] { null };
            foreach (var testCase in cases)
            {
                discovered = true;
                object[] values = testCase?.Arguments ?? Array.Empty<object>();
                string name = fixture.FullName + "." + method.Name + (values.Length == 0 ? "" : "(" + string.Join(",", values.Select(v => Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture))) + ")");
                string error = setupError ?? (unsupported == null ? null : "Unsupported NUnit attribute: " + unsupported);
                if (error == null)
                {
                    try
                    {
                        if (method.ContainsGenericParameters) throw new NotSupportedException("Generic test method is unsupported.");
                        var parameters = method.GetParameters();
                        if (parameters.Length != values.Length) throw new InvalidOperationException("Test argument count mismatch.");
                        object[] converted = values.Select((value, index) => value == null || parameters[index].ParameterType.IsInstanceOfType(value) ? value
                            : parameters[index].ParameterType.IsEnum ? Enum.ToObject(parameters[index].ParameterType, value)
                            : Convert.ChangeType(value, parameters[index].ParameterType, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                        object target = method.IsStatic ? null : Activator.CreateInstance(fixture, true);
                        object returned = method.Invoke(target, converted);
                        if (returned is Task task) { task.GetAwaiter().GetResult(); returned = task.GetType().GetProperty("Result")?.GetValue(task); }
                        else if (returned is System.Collections.IEnumerator) throw new NotSupportedException("Enumerator tests require Unity/NUnit execution.");
                        if (testCase?.HasExpectedResult == true) Assert.That(returned, Is.EqualTo(testCase.ExpectedResult));
                        else if (method.ReturnType != typeof(void) && !typeof(Task).IsAssignableFrom(method.ReturnType)) throw new NotSupportedException("Non-void test without ExpectedResult.");
                    }
                    catch (Exception exception) { error = (exception is TargetInvocationException invocation ? invocation.InnerException ?? invocation : exception).ToString(); }
                }
                yield return new TestResult(name, error == null, error);
            }
        }
        if (!discovered) yield return new TestResult(fixture.FullName, false, "Fixture contains no supported test cases.");
    }

    private static bool CheckRunner()
    {
        var normal = RunFixture(typeof(RunnerProbe)).ToArray();
        var setup = RunFixture(typeof(UnsupportedSetupProbe)).ToArray();
        return normal.Length == 3 && normal.Count(r => r.passed) == 2 && normal.Single(r => !r.passed).name.EndsWith("ExpectedFailure", StringComparison.Ordinal)
            && setup.Length == 1 && !setup[0].passed && setup[0].error.Contains("lifecycle", StringComparison.Ordinal);
    }
    private sealed class RunnerProbe
    {
        [TestCase(1), TestCase(2)] public void CasesPass(int value) { Assert.That(value, Is.GreaterThan(0)); }
        [Test] public void ExpectedFailure() { Assert.Fail("Runner must record this failure."); }
    }
    private sealed class UnsupportedSetupProbe
    {
        [SetUp] public void Setup() { }
        [Test] public void NotExecuted() { throw new InvalidOperationException("Lifecycle rejection must happen first."); }
    }

    private static void Capture(string name, Func<object> scenario, List<object> measurements, List<string> failures)
    {
        try { measurements.Add(scenario()); }
        catch (Exception error) { failures.Add(name + ": " + error); }
    }
    private static object Measure(string name, object conditions, Action operation, Func<object> verify, Action reset = null)
    {
        for (int index = 0; index < warmup; index++) operation();
        reset?.Invoke();
        var samples = new List<Batch>(batches);
        for (int batch = 0; batch < batches; batch++)
        {
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            long before = GC.GetAllocatedBytesForCurrentThread(), started = Stopwatch.GetTimestamp();
            for (int index = 0; index < repetitions; index++) operation();
            long elapsed = Stopwatch.GetTimestamp() - started;
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            samples.Add(new Batch(repetitions, elapsed, elapsed * 1000.0 / Stopwatch.Frequency / repetitions, bytes, (double)bytes / repetitions));
        }
        return new { name, conditions, warmup, repetitions, batches,
            medianMillisecondsPerOperation = samples.Select(s => s.millisecondsPerOperation).OrderBy(v => v).ElementAt(batches / 2),
            medianBytesPerOperation = samples.Select(s => s.bytesPerOperation).OrderBy(v => v).ElementAt(batches / 2),
            samples, observations = verify() };
    }
    private static object Collector(int count)
    {
        using var rig = RunTestRig.Create(seed: 260917, enableAi: false, enableCombat: true);
        var positions = new Dictionary<long, WorldPosition>();
        for (int index = 0; index < count; index++)
        {
            double angle = index * Math.PI * 2 / Math.Max(1, count);
            var position = rig.Player.Position.Offset(new DVec2(Math.Cos(angle), Math.Sin(angle)) * .75);
            positions.Add(rig.World.AddExperience(position, 1, createdAt: 1), position);
        }
        var collector = new ExperienceCollector(rig.Run, rig.Movement, new ProgressionService(rig.Run));
        return Measure("collector-xp-" + count, new { experience = count, enemies = 0, stepSeconds = .02,
            clockSeconds = 0, createdAt = 1, radius = .75, attractionRadius = rig.Run.PickupSettings.AttractionRadius,
            ai = false, combatConstructed = true, simulationAdvanced = false }, () => collector.Step(.02), () => {
                if (rig.Clock.ElapsedSeconds != 0 || rig.World.Experience.Count != count || rig.World.AttractingExperienceIds.Count != 0
                    || positions.Any(pair => !rig.World.TryGetExperience(pair.Key, out var orb) || orb.State != ExperienceState.Grounded || !orb.Position.Equals(pair.Value)))
                    throw new InvalidOperationException("Collector scenario mutated clock or experience state.");
                return new { units = rig.Run.Units.Count, experience = rig.World.Experience.Count, attracting = rig.World.AttractingExperienceIds.Count, stateUnchanged = true };
            });
    }
    private static object Presenter(int enemies)
    {
        using var rig = RunTestRig.Create(seed: 260917, enableAi: false, enableCombat: true);
        for (int index = 0; index < enemies; index++) rig.Spawn(UnitKind.Normal, new DVec2((index % 25 - 12) * .58, (index / 25 - 10) * .58));
        for (int index = 0; index < 200; index++) rig.DropXp(new DVec2((index % 20 - 10) * .4, (index / 20 - 5) * .4), 1);
        for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++) rig.World.GetChunk(new ChunkCoord(x, y));
        var view = new NoopCombatView(); var presenter = new WorldPresenter(view);
        return Measure("presenter-units-" + enemies, new { enemies, totalUnits = enemies + 1, experience = 200,
            viewRadius = 60, interpolation = 1, cachedChunks = 9, spacing = .58, rendering = false, ai = false, simulationAdvanced = false },
            () => presenter.Refresh(rig.Run, 1, 60), () => {
                long operations = (long)repetitions * batches;
                if (rig.Run.Units.Count != enemies + 1 || rig.World.Experience.Count != 200 || view.frames != operations
                    || view.units != operations * (enemies + 1) || view.experience != operations * 200 || view.enemies != enemies)
                    throw new InvalidOperationException("Presenter population or view callback counts changed.");
                return new { units = rig.Run.Units.Count, experience = rig.World.Experience.Count, refreshes = view.frames,
                    showUnitCalls = view.units, showExperienceCalls = view.experience, enemiesReported = view.enemies };
            }, view.Reset);
    }
    private sealed class NoopCombatView : ICombatWorldView
    {
        public long frames, units, experience;
        public int enemies;
        public void Reset() { frames = units = experience = 0; enemies = 0; }
        public void BeginFrame(Guid runId) { frames++; }
        public void ShowTerrain(ChunkData chunk, DVec2 relativeOrigin) { }
        public void ShowUnit(UnitModel unit, DVec2 relativePosition) { units++; }
        public void EndFrame(double elapsedSeconds, int enemyCount) { enemies = enemyCount; }
        public void SetFrameTime(double time) { }
        public void ShowExperience(ExperienceRecord record, ExperienceTier tier, DVec2 relative, double radius) { experience++; }
        public void ShowAttack(AttackShapeSnapshot shape, DVec2 relative) { }
        public void ShowProjectile(ProjectileModel projectile, DVec2 relative) { }
        public void ShowExplosion(ExplosionSnapshot explosion, DVec2 relative, double lifetime) { }
    }
}
