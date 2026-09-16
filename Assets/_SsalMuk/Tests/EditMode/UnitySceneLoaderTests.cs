using System;
using NUnit.Framework;
using SsalMuk.Unity;
using UnityEngine.SceneManagement;

namespace SsalMuk.Tests
{
    public sealed class UnitySceneLoaderTests
    {
        [Test]
        public void MissingSceneFailsBeforeChangingTheActiveScene()
        {
            var originalScene = SceneManager.GetActiveScene().handle;
            var loader = new UnitySceneLoader("Assets/_SsalMuk/Scenes/DoesNotExist.unity");
            Assert.Throws<InvalidOperationException>(() => loader.LoadBattleAsync());
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(originalScene));
        }
    }
}
