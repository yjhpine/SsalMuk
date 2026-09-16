using System;
using System.Threading.Tasks;
using SsalMuk.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SsalMuk.Unity
{
    public sealed class UnitySceneLoader : ISceneLoader
    {
        private readonly string battleScenePath;
        public UnitySceneLoader(string battleScenePath = AppRoot.BattleScenePath)
        {
            if (string.IsNullOrWhiteSpace(battleScenePath)) throw new ArgumentException("A battle scene path is required.", nameof(battleScenePath));
            this.battleScenePath = battleScenePath;
        }
        public Task LoadBattleAsync() => LoadAsync(battleScenePath);
        public Task LoadMainMenuAsync() => LoadAsync(AppRoot.MainMenuScenePath);
        private static Task LoadAsync(string scenePath)
        {
            if (SceneManager.GetActiveScene().path == scenePath) return Task.CompletedTask;
            if (!Application.CanStreamedLevelBeLoaded(scenePath)) throw new InvalidOperationException("Scene is missing from the build scene list: " + scenePath);
            var operation = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single);
            if (operation == null) throw new InvalidOperationException("Battle scene loading could not start.");
            if (operation.isDone) return Task.CompletedTask;
            var completion = new TaskCompletionSource<bool>();
            operation.completed += _ => completion.TrySetResult(true); return completion.Task;
        }
    }
}
