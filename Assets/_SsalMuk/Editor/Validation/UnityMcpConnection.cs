using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace SsalMuk.Editor.Validation
{
    [InitializeOnLoad]
    internal static class UnityMcpConnection
    {
        private static bool connecting;
        private static double nextPoll;

        static UnityMcpConnection()
        {
            if (!AssetDatabase.IsAssetImportWorkerProcess()) EditorApplication.update += Update;
        }

        private static async void Update()
        {
            if (connecting || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 1;
            string requestPath = Path.Combine(ValidationFiles.Results, "connect-mcp.json");
            if (!File.Exists(requestPath)) return;
            connecting = true;
            var result = new ConnectionResult();
            try
            {
                var request = JsonUtility.FromJson<ConnectionRequest>(File.ReadAllText(requestPath));
                File.Delete(requestPath);
                result.requestId = request.requestId;
                if (!ValidationRequest.IsRunId(request.requestId) ||
                    DateTimeOffset.UtcNow > DateTimeOffset.Parse(request.requestedUtc).AddMinutes(2))
                    throw new InvalidDataException("Connection request is invalid or expired.");
                string url = EditorPrefs.GetString("MCPForUnity.HttpUrl", "http://127.0.0.1:8080").TrimEnd('/');
                if (string.IsNullOrEmpty(url)) url = "http://127.0.0.1:8080";
                if (!EditorPrefs.GetBool("MCPForUnity.UseHttpTransport", true) ||
                    EditorPrefs.GetString("MCPForUnity.HttpTransportScope", "local") == "remote" ||
                    (url != "http://127.0.0.1:8080" && url != "http://localhost:8080"))
                    throw new InvalidOperationException("Existing Unity MCP settings do not target local HTTP port 8080.");
                var type = Type.GetType("MCPForUnity.Editor.Services.MCPServiceLocator, MCPForUnity.Editor", true);
                var bridge = type.GetProperty("Bridge").GetValue(null);
                bool started = await (Task<bool>)bridge.GetType().GetMethod("StartAsync").Invoke(bridge, null);
                if (!started) throw new InvalidOperationException("Unity MCP bridge did not connect.");
                result.status = "Connected";
            }
            catch (Exception error) { result.status = "Failed"; result.message = error.GetBaseException().Message; }
            finally
            {
                result.completedUtc = DateTime.UtcNow.ToString("O");
                result.projectPath = ValidationFiles.Root.Replace('\\', '/');
                result.unityVersion = Application.unityVersion;
                ValidationFiles.WriteJson(Path.Combine(ValidationFiles.Results, "mcp-connection.json"), result);
                connecting = false;
            }
        }

        [Serializable] private sealed class ConnectionRequest { public string requestId, requestedUtc; }
        [Serializable] private sealed class ConnectionResult
        {
            public string requestId, status, message, completedUtc, projectPath, unityVersion;
        }
    }
}
