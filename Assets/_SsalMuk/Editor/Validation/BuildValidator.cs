using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using SsalMuk.Unity;
namespace SsalMuk.Editor.Validation
{
    public static class BuildValidator
    {
        private static bool queued;
        public static string QueueWindows(string runId, string sourceHash)
        {
            if (!ValidationRequest.IsRunId(runId) || sourceHash != ValidationFiles.SourceHash()) throw new InvalidOperationException("Build source identity is not current.");
            if (queued || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("The Editor is busy.");
            for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("A scene has unsaved changes.");
            queued = true;
            string directory = ValidationFiles.RunDirectory(runId); Directory.CreateDirectory(directory);
            var evidence = new BuildEvidence { runId = runId, sourceHash = sourceHash, status = "Pending", requestedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion, executable = Path.Combine(ValidationFiles.Root, "Builds/SsalMuk/SsalMuk.exe"),
                scenes = new[] { AppRoot.MainMenuScenePath, AppRoot.BattleScenePath }, development = true };
            ValidationFiles.WriteJson(Path.Combine(directory, "build-request.json"), evidence);
            EditorApplication.CallbackFunction execute = null;
            execute = () =>
            {
                EditorApplication.update -= execute;
                try
                {
                    evidence.startedUtc = DateTime.UtcNow.ToString("O");
                    evidence.buildSettingsBefore = ValidationFiles.FileHash(Path.Combine(ValidationFiles.Root, "ProjectSettings/EditorBuildSettings.asset"));
                    Directory.CreateDirectory(Path.GetDirectoryName(evidence.executable));
                    // Unity serializes shader filters, player batching and service settings while building.
                    // Preserve their exact input bytes, including the user's pre-existing changes.
                    evidence.preservedFiles = PreserveBuildFiles(directory);
                    BuildReport report;
                    try
                    {
                        report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = evidence.scenes, locationPathName = evidence.executable,
                            target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
                    }
                    finally { RestoreBuildFiles(directory, evidence.preservedFiles); }
                    evidence.result = report.summary.result.ToString(); evidence.totalBytes = (long)report.summary.totalSize; evidence.durationSeconds = report.summary.totalTime.TotalSeconds;
                    evidence.errors = report.summary.totalErrors;
                    evidence.buildSettingsAfter = ValidationFiles.FileHash(Path.Combine(ValidationFiles.Root, "ProjectSettings/EditorBuildSettings.asset"));
                    if (report.summary.result != BuildResult.Succeeded || report.summary.totalErrors != 0 || !File.Exists(evidence.executable))
                        throw new InvalidOperationException("Windows build failed: " + report.summary.result);
                    if (evidence.buildSettingsBefore != evidence.buildSettingsAfter || ValidationFiles.SourceHash() != sourceHash)
                        throw new InvalidOperationException("Project settings or sources changed during the build.");
                    evidence.executableSha256 = ValidationFiles.FileHash(evidence.executable); evidence.status = "Passed";
                }
                catch (Exception error) { evidence.status = "Failed"; evidence.message = error.ToString(); }
                finally { queued = false; evidence.completedUtc = DateTime.UtcNow.ToString("O"); ValidationFiles.WriteJson(Path.Combine(directory, "build.json"), evidence); }
            };
            EditorApplication.update += execute;
            return directory;
        }
        private static PreservedFile[] PreserveBuildFiles(string directory)
        {
            string[] paths = { "ProjectSettings/ProjectSettings.asset", "ProjectSettings/GraphicsSettings.asset",
                "ProjectSettings/UnityConnectSettings.asset", "Assets/DefaultVolumeProfile.asset",
                "Assets/Settings/UniversalRP.asset", "Assets/UniversalRenderPipelineGlobalSettings.asset" };
            return paths.Where(p => File.Exists(Path.Combine(ValidationFiles.Root, p))).Select(p =>
            {
                string original = Path.Combine(ValidationFiles.Root, p), backup = Path.Combine(directory, "settings-before", p);
                Directory.CreateDirectory(Path.GetDirectoryName(backup)); File.Copy(original, backup);
                return new PreservedFile { path = p, beforeHash = ValidationFiles.FileHash(original) };
            }).ToArray();
        }
        private static void RestoreBuildFiles(string directory, PreservedFile[] files)
        {
            foreach (var file in files)
            {
                string original = Path.Combine(ValidationFiles.Root, file.path), backup = Path.Combine(directory, "settings-before", file.path);
                if (ValidationFiles.FileHash(backup) != file.beforeHash) throw new InvalidOperationException("Build settings backup changed.");
                if (!File.Exists(original) || ValidationFiles.FileHash(original) != file.beforeHash) File.Copy(backup, original, true);
                file.afterHash = ValidationFiles.FileHash(original);
                if (file.afterHash != file.beforeHash) throw new InvalidOperationException("Build did not preserve " + file.path);
            }
        }
        [Serializable]
        private sealed class PreservedFile { public string path, beforeHash, afterHash; }
        [Serializable]
        private sealed class BuildEvidence
        {
            public string runId, sourceHash, status, result, message, requestedUtc, startedUtc, completedUtc, unityVersion, executable, executableSha256, buildSettingsBefore, buildSettingsAfter;
            public string[] scenes;
            public PreservedFile[] preservedFiles;
            public bool development;
            public int errors;
            public long totalBytes;
            public double durationSeconds;
        }
    }
}
