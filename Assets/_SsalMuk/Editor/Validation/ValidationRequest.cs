using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace SsalMuk.Editor.Validation
{
    [Serializable]
    public sealed class ValidationRequest
    {
        public int schemaVersion;
        public string runId, projectPath, sourceHash, mode, filter, scenario, startedUtc;
        public int timeoutSeconds;
        public bool IsScenario => mode == "Smoke" || mode == "Stress";

        public void Validate(string expectedProject, string directoryId)
        {
            if (schemaVersion != 1 || !IsRunId(runId) || runId != directoryId)
                throw new InvalidDataException("Invalid request identity.");
            if (string.IsNullOrEmpty(projectPath) || !Path.IsPathRooted(projectPath) ||
                !string.Equals(Path.GetFullPath(projectPath).TrimEnd('/', '\\'),
                    Path.GetFullPath(expectedProject).TrimEnd('/', '\\'), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Request is from another project.");
            if (!Regex.IsMatch(sourceHash ?? "", "^[a-f0-9]{64}$") ||
                (mode != "EditMode" && mode != "PlayMode" && !IsScenario) ||
                (!string.IsNullOrEmpty(filter) && !Regex.IsMatch(filter, "^[A-Za-z_][A-Za-z0-9_.]{0,255}$")))
                throw new InvalidDataException("Unsupported validation request.");
            if (IsScenario)
            {
                if (!string.IsNullOrEmpty(filter) || SsalMuk.Unity.Diagnostics.ScenarioDefinition.Get(scenario).Mode != mode)
                    throw new InvalidDataException("Scenario and mode do not match.");
            }
            else if (!string.IsNullOrEmpty(scenario)) throw new InvalidDataException("Only Smoke/Stress accept a scenario.");
            if (timeoutSeconds < 1 || timeoutSeconds > 14400 ||
                !DateTimeOffset.TryParse(startedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var start) ||
                start > DateTimeOffset.UtcNow || DateTimeOffset.UtcNow > start.AddSeconds(timeoutSeconds))
                throw new InvalidDataException("Request is expired or has invalid timing.");
        }

        public static bool IsRunId(string value) => Regex.IsMatch(value ?? "", "^[a-f0-9]{32}$");
    }
}
