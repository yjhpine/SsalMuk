using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SsalMuk.Tests
{
    public sealed class ValidationPlayModeTests
    {
        [UnityTest]
        public IEnumerator PlayerLoopIsActuallyRunning()
        {
            Assert.That(Application.isPlaying, Is.True);
            var frame = Time.frameCount;
            yield return null;
            Assert.That(Time.frameCount, Is.GreaterThan(frame));
        }
    }
}
