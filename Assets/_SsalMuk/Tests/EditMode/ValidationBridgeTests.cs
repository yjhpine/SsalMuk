using System;
using System.IO;
using NUnit.Framework;
using SsalMuk.Editor.Validation;
using UnityEngine;

namespace SsalMuk.Tests
{
    public sealed class ValidationBridgeTests
    {
        private static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private static ValidationRequest Valid()
        {
            return new ValidationRequest {
                schemaVersion = 1, runId = "0123456789abcdef0123456789abcdef",
                projectPath = Root, sourceHash = new string('a', 64), mode = "EditMode",
                filter = "SsalMuk.Tests.ValidationBridgeTests", startedUtc = DateTime.UtcNow.ToString("O"), timeoutSeconds = 60
            };
        }

        [Test]
        public void RoundTrip()
        {
            var request = Valid();
            var restored = JsonUtility.FromJson<ValidationRequest>(JsonUtility.ToJson(request));
            Assert.DoesNotThrow(() => restored.Validate(Root, request.runId));
            Assert.That(restored.runId, Is.EqualTo(request.runId));
            Assert.That(restored.filter, Is.EqualTo(request.filter));
        }

        [Test]
        public void ForeignProjectCannotExecute()
        {
            var request = Valid(); request.projectPath = Path.Combine(Root, "Other");
            Assert.Throws<InvalidDataException>(() => request.Validate(Root, request.runId));
        }

        [Test]
        public void TraversalCannotBecomeAResultPath()
        {
            var request = Valid(); request.runId = "../../Other";
            Assert.Throws<InvalidDataException>(() => request.Validate(Root, request.runId));
        }

        [Test]
        public void UnsupportedScenarioCannotPretendToBeATest()
        {
            var request = Valid(); request.mode = "Smoke";
            Assert.Throws<InvalidDataException>(() => request.Validate(Root, request.runId));
        }

        [Test]
        public void FilterDoesNotInterpretRegularExpressions()
        {
            var request = Valid(); request.filter = "SsalMuk.*";
            Assert.Throws<InvalidDataException>(() => request.Validate(Root, request.runId));
        }

        [Test]
        public void ExpiredRequestCannotStartLater()
        {
            var request = Valid(); request.startedUtc = DateTime.UtcNow.AddMinutes(-10).ToString("O");
            Assert.Throws<InvalidDataException>(() => request.Validate(Root, request.runId));
        }
    }
}
