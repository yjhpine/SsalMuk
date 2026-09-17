using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SsalMuk.Unity;
using SsalMuk.Unity.Diagnostics;
using UnityEngine;
using UnityEngine.TestTools;
namespace SsalMuk.Tests
{
    public sealed class EndToEndTests
    {
        private GameObject host;
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (host != null) UnityEngine.Object.Destroy(host);
            if (AppRoot.Instance != null) UnityEngine.Object.Destroy(AppRoot.Instance.gameObject);
            yield return null; yield return null;
        }
        [UnityTest]
        public IEnumerator ScenarioUsesRealButtonsFlightContactRewardDeathAndRestart()
        {
            string id = Guid.NewGuid().ToString("N");
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/Validation/ScenarioTests", id));
            host = new GameObject("Scenario test"); UnityEngine.Object.DontDestroyOnLoad(host);
            var scenario = host.AddComponent<ScenarioRunner>();
            yield return scenario.Run("EndToEnd", directory, id, new string('a', 64));
            Assert.That(scenario.Report.status, Is.EqualTo("Passed"), scenario.Report.message);
            Assert.That(scenario.Report.observations, Is.SupersetOf(new[] { "GameStart", "Attack", "ExperienceGrounded", "ExperienceAttracting", "ExperienceFlight", "ExperienceContact", "ChoiceButton", "Death", "Results", "Restart", "CleanScope" }));
            Assert.That(scenario.Report.samples.Select(x => x.runId).Distinct().Count(), Is.GreaterThanOrEqualTo(2));
            Assert.That(scenario.Report.metrics.tickCount, Is.GreaterThan(0));
            Assert.That(File.Exists(Path.Combine(directory, "scenario.json")), Is.True);
        }
    }
}
