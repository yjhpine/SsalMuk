using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace SsalMuk.Editor.Validation
{
    internal static class ValidationFiles
    {
        internal static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        internal static string Results => Path.Combine(Root, "Logs", "Validation");
        internal static string RunDirectory(string id)
        {
            if (!ValidationRequest.IsRunId(id)) throw new InvalidDataException("Invalid run directory.");
            return Path.Combine(Results, id);
        }

        internal static void WriteJson(string path, object value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(value, true), new UTF8Encoding(false));
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    if (File.Exists(path)) File.Replace(temporary, path, null);
                    else File.Move(temporary, path);
                    return;
                }
                catch (IOException) when (attempt < 4 && File.Exists(temporary))
                {
                    // Windows readers/scanners may briefly hold the destination during a reload.
                    // Keep atomic replacement; persistent failures still fail the validation run.
                    System.Threading.Thread.Sleep(20 * (attempt + 1));
                }
            }
        }

        internal static string FileHash(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            return Hex(sha.ComputeHash(stream));
        }

        internal static string SourceHash()
        {
            string root = Root;
            var names = new List<string>();
            foreach (string directory in new[] { "Assets/_SsalMuk", "ProjectSettings", "tools/validation" })
            {
                string path = Path.Combine(root, directory);
                if (!Directory.Exists(path)) continue;
                foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                    names.Add(file.Substring(root.Length + 1).Replace('\\', '/'));
            }
            foreach (string file in new[] { "Assets/_SsalMuk.meta", "Packages/manifest.json", "Packages/packages-lock.json" })
                if (File.Exists(Path.Combine(root, file))) names.Add(file);
            names.Sort(StringComparer.Ordinal);
            var input = new StringBuilder();
            foreach (string name in names)
                input.Append(name).Append('\0').Append(FileHash(Path.Combine(root, name))).Append('\n');
            using var sha = SHA256.Create();
            return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(input.ToString())));
        }

        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }

    [Serializable]
    internal sealed class ActiveValidation
    {
        public ValidationRequest request;
        public string startedUtc, jobId;
        public string[] discoveredNames = Array.Empty<string>();
        public int errorCount;
        public bool resultSaved, testsPassed;
        public string resultMessage, xmlHash;
    }

    [Serializable]
    internal sealed class ValidationReceipt
    {
        public int schemaVersion = 1;
        public string runId, sourceHash, projectPath, mode, filter, requestedUtc, startedUtc, completedUtc;
        public string status, failureKind, message, xmlSha256, unityVersion;
        public int errorCount;
        public string[] discoveredNames;
    }

    [Serializable]
    internal sealed class ValidationEditorStatus
    {
        public string updatedUtc, projectPath, unityVersion, activeRunId;
        public bool compilationFailed, compiling, playing;
        public int processId;
    }
}
