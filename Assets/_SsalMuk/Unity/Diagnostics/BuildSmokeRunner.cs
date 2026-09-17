using System;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
namespace SsalMuk.Unity.Diagnostics
{
    public sealed class BuildSmokeRunner : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
            string[] args = Environment.GetCommandLineArgs();
            string scenario = Argument(args, "-ssalmukScenario");
            if (scenario == null) return;
            string output = Argument(args, "-ssalmukOutput"), id = Argument(args, "-ssalmukRun"), hash = Argument(args, "-ssalmukSource");
            if (string.IsNullOrEmpty(output) || !Regex.IsMatch(id ?? "", "^[a-f0-9]{32}$") || !Regex.IsMatch(hash ?? "", "^[a-f0-9]{64}$"))
            { Debug.LogError("Invalid scenario arguments."); Application.Quit(2); return; }
            Application.runInBackground = true;
            var host = new GameObject("Standalone verification"); DontDestroyOnLoad(host);
            host.AddComponent<BuildSmokeRunner>().StartCoroutine(Execute(host, scenario, output, id, hash));
#endif
        }
        private static string Argument(string[] args, string flag)
        { int at = Array.IndexOf(args, flag); return at >= 0 && at + 1 < args.Length ? args[at + 1] : null; }
        private static IEnumerator Execute(GameObject host, string scenario, string output, string id, string hash)
        {
            // The native startup splash can cover early rendered frames in a fast smoke run.
            while (!UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            yield return null;
            var runner = host.AddComponent<ScenarioRunner>();
            yield return runner.Run(scenario, output, id, hash);
            Application.Quit(runner.Report?.status == "Passed" ? 0 : 1);
        }
    }
}
