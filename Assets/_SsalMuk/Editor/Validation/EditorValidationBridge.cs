using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SsalMuk.Editor.Validation
{
    [InitializeOnLoad]
    public static class EditorValidationBridge
    {
        private const string SessionKey = "SsalMuk.Validation.Active";
        // Test Framework 1.6.0 exposes cancellation but keeps the busy query internal.
        // Fail closed if this pinned API changes instead of overlapping another test run.
        private static readonly MethodInfo BusyQuery = typeof(TestRunnerApi).GetMethod(
            "IsRunActive", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly ValidationCallbacks Callbacks = new ValidationCallbacks();
        private static TestRunnerApi api;
        private static ActiveValidation active;
        private static double nextPoll, nextHeartbeat;
        private static int? batchExit;

        static EditorValidationBridge()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess()) return;
            string saved = SessionState.GetString(SessionKey, "");
            if (!string.IsNullOrEmpty(saved)) active = JsonUtility.FromJson<ActiveValidation>(saved);
            TestRunnerApi.RegisterTestCallback(Callbacks);
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Update;
        }

        public static void ExecuteBatch()
        {
            if (string.IsNullOrEmpty(BatchRunId()))
                throw new InvalidDataException("Missing -ssalmukValidationRun identity.");
            // InitializeOnLoad processes this request and exits only after test cleanup.
        }

        private static string BatchRunId()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-ssalmukValidationRun");
            return index >= 0 && index + 1 < args.Length && ValidationRequest.IsRunId(args[index + 1])
                ? args[index + 1] : "";
        }

        private static bool TestsBusy()
        {
            if (BusyQuery == null) throw new InvalidOperationException("Test Framework busy query is unavailable.");
            return (bool)BusyQuery.Invoke(null, null);
        }

        private static void Update()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 0.3;
            try
            {
                if (EditorApplication.timeSinceStartup >= nextHeartbeat)
                {
                    nextHeartbeat = EditorApplication.timeSinceStartup + 2;
                    ValidationFiles.WriteJson(Path.Combine(ValidationFiles.Results, "editor-status.json"), new ValidationEditorStatus {
                        updatedUtc = DateTime.UtcNow.ToString("O"), projectPath = ValidationFiles.Root.Replace('\\', '/'),
                        unityVersion = Application.unityVersion, processId = System.Diagnostics.Process.GetCurrentProcess().Id,
                        compilationFailed = EditorUtility.scriptCompilationFailed, compiling = EditorApplication.isCompiling,
                        playing = EditorApplication.isPlaying, activeRunId = active?.request.runId ?? ""
                    });
                }
                if (batchExit.HasValue && !TestsBusy() && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorApplication.Exit(batchExit.Value);
                    return;
                }
                if (active != null)
                {
                    string directory = ValidationFiles.RunDirectory(active.request.runId);
                    if (File.Exists(Path.Combine(directory, "cancel.json"))) Cancel("Cancelled", "Runner withdrew this request.");
                    else if (DateTimeOffset.UtcNow > DateTimeOffset.Parse(active.request.startedUtc).AddSeconds(active.request.timeoutSeconds))
                        Cancel("Timeout", "Test execution exceeded the request deadline.");
                    else if (EditorUtility.scriptCompilationFailed) Cancel("CompilationFailed", "Unity reports script compilation errors.");
                    else if (active.resultSaved && !TestsBusy() && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isUpdating)
                    {
                        // PlayMode reports its result before restoring scenes and project settings.
                        // Compare sources only after that restoration, and preserve this state across reloads.
                        if (ValidationFiles.SourceHash() != active.request.sourceHash)
                            Fail("SourceChanged", "Sources changed during execution.");
                        else
                        {
                            bool passed = active.testsPassed && active.errorCount == 0;
                            Finish(passed ? "Passed" : "Failed", passed ? "" : "TestsFailed", active.resultMessage, active.xmlHash);
                        }
                    }
                    return;
                }
                if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                    EditorApplication.isPlayingOrWillChangePlaymode || TestsBusy()) return;
                if (!Directory.Exists(ValidationFiles.Results)) return;
                foreach (string directory in Directory.GetDirectories(ValidationFiles.Results).OrderBy(x => x, StringComparer.Ordinal))
                {
                    string id = Path.GetFileName(directory);
                    if (!ValidationRequest.IsRunId(id) || (Application.isBatchMode && BatchRunId() != id)) continue;
                    string path = Path.Combine(directory, "request.json");
                    if (!File.Exists(path) || File.Exists(Path.Combine(directory, "receipt.json")) ||
                        File.Exists(Path.Combine(directory, "cancel.json"))) continue;
                    Accept(path, id);
                    break;
                }
            }
            catch (Exception error)
            {
                if (active != null) Fail("BridgeError", error.ToString());
                else
                {
                    // Avoid emitting the same infrastructure failure every editor frame.
                    nextPoll = EditorApplication.timeSinceStartup + 10;
                    Debug.LogError("SsalMuk validation bridge: " + error.Message);
                }
            }
        }

        private static void Accept(string path, string id)
        {
            var request = JsonUtility.FromJson<ValidationRequest>(File.ReadAllText(path));
            File.Move(path, Path.Combine(ValidationFiles.RunDirectory(id), "running.json"));
            // Always use the validated directory identity for output paths, including rejected input.
            if (request == null) request = new ValidationRequest();
            string suppliedId = request.runId;
            request.runId = id;
            active = new ActiveValidation { request = request, startedUtc = DateTime.UtcNow.ToString("O") };
            Persist();
            try
            {
                if (suppliedId != id) throw new InvalidDataException("Request identity does not match its directory.");
                request.Validate(ValidationFiles.Root, id);
                if (EditorUtility.scriptCompilationFailed) { Fail("CompilationFailed", "Unity reports script compilation errors."); return; }
                if (ValidationFiles.SourceHash() != request.sourceHash) { Fail("SourceChanged", "Sources changed before execution."); return; }
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).isDirty) { Fail("EditorBusy", "Save the open scene before running validation."); return; }
                api = ScriptableObject.CreateInstance<TestRunnerApi>();
                TestMode mode = request.mode == "EditMode" ? TestMode.EditMode : TestMode.PlayMode;
                api.RetrieveTestList(mode, tree => StartTests(id, mode, tree));
            }
            catch (Exception error) { Fail("InvalidRequest", error.Message); }
        }

        private static void StartTests(string id, TestMode mode, ITestAdaptor tree)
        {
            if (active?.request.runId != id) return;
            try
            {
                var request = active.request;
                request.Validate(ValidationFiles.Root, id);
                string assembly = "SsalMuk.Tests." + request.mode;
                string pattern = string.IsNullOrEmpty(request.filter) ? "" : "^" + Regex.Escape(request.filter) + @"(?:\.|$)";
                active.discoveredNames = Discover(tree, assembly, pattern, false).OrderBy(x => x, StringComparer.Ordinal).ToArray();
                if (active.discoveredNames.Length == 0) { Fail("NoTests", "No tests match this assembly and filter."); return; }
                if (TestsBusy()) { Fail("EditorBusy", "Another test run started during discovery."); return; }
                Persist();
                string job = api.Execute(new ExecutionSettings(new Filter {
                    testMode = mode, assemblyNames = new[] { assembly },
                    groupNames = string.IsNullOrEmpty(pattern) ? null : new[] { pattern }
                }));
                if (active?.request.runId == id) { active.jobId = job; Persist(); }
            }
            catch (Exception error) { Fail("TestExecutionFailed", error.Message); }
        }

        private static IEnumerable<string> Discover(ITestAdaptor test, string assembly, string pattern, bool selected)
        {
            if (test.IsTestAssembly) selected = Path.GetFileNameWithoutExtension(test.Name) == assembly;
            if (!test.IsSuite && selected && (pattern.Length == 0 || Regex.IsMatch(test.FullName, pattern))) yield return test.FullName;
            foreach (var child in test.Children ?? Enumerable.Empty<ITestAdaptor>())
                foreach (string name in Discover(child, assembly, pattern, selected)) yield return name;
        }

        private static IEnumerable<ITestResultAdaptor> Leaves(ITestResultAdaptor result)
        {
            if (!result.Test.IsSuite) yield return result;
            foreach (var child in result.Children ?? Enumerable.Empty<ITestResultAdaptor>())
                foreach (var leaf in Leaves(child)) yield return leaf;
        }

        internal static void Complete(ITestResultAdaptor result)
        {
            if (active == null || active.resultSaved || active.discoveredNames.Length == 0) return;
            try
            {
                string path = Path.Combine(ValidationFiles.RunDirectory(active.request.runId), active.request.mode.ToLowerInvariant() + ".xml");
                TestRunnerApi.SaveResultToFile(result, path);
                var leaves = Leaves(result).ToArray();
                active.testsPassed = result.ResultState == "Passed" && active.errorCount == 0 &&
                    leaves.All(x => x.ResultState == "Passed") &&
                    leaves.Select(x => x.FullName).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(active.discoveredNames);
                active.xmlHash = ValidationFiles.FileHash(path);
                active.resultMessage = result.Message;
                active.resultSaved = true;
                Persist();
            }
            catch (Exception error) { Fail("ResultWriteFailed", error.Message); }
        }

        private static void OnLog(string message, string stack, LogType type)
        {
            if (active == null || (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
            active.errorCount++;
            Persist();
        }

        private static void Persist()
        {
            SessionState.SetString(SessionKey, JsonUtility.ToJson(active));
            ValidationFiles.WriteJson(Path.Combine(ValidationFiles.RunDirectory(active.request.runId), "active.json"), active);
        }

        private static void Cancel(string kind, string message)
        {
            string job = active?.jobId;
            Fail(kind, message);
            if (!string.IsNullOrEmpty(job)) TestRunnerApi.CancelTestRun(job);
        }

        internal static void Fail(string kind, string message)
        {
            if (active != null) Finish("Failed", kind, message, "");
        }

        private static void Finish(string status, string kind, string message, string xmlHash)
        {
            var request = active.request;
            ValidationFiles.WriteJson(Path.Combine(ValidationFiles.RunDirectory(request.runId), "receipt.json"), new ValidationReceipt {
                runId = request.runId, projectPath = ValidationFiles.Root.Replace('\\', '/'), sourceHash = request.sourceHash,
                mode = request.mode, filter = request.filter ?? "", requestedUtc = request.startedUtc,
                startedUtc = active.startedUtc, completedUtc = DateTime.UtcNow.ToString("O"), status = status,
                failureKind = kind, message = message ?? "", errorCount = active.errorCount,
                discoveredNames = active.discoveredNames, xmlSha256 = xmlHash, unityVersion = Application.unityVersion
            });
            active = null;
            SessionState.EraseString(SessionKey);
            if (api != null) UnityEngine.Object.DestroyImmediate(api);
            api = null;
            if (Application.isBatchMode) batchExit = status == "Passed" ? 0 : 1;
        }
    }
}
