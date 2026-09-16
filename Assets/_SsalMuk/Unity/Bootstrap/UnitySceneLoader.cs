using System;
using System.Threading.Tasks;
using SsalMuk.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SsalMuk.Unity
{
    public sealed class UnitySceneLoader : ISceneLoader
    {
        public Task LoadBattleAsync()
        {
            if (SceneManager.GetActiveScene().path == AppRoot.BattleScenePath) return Task.CompletedTask;
            if (!Application.CanStreamedLevelBeLoaded(AppRoot.BattleScenePath)) throw new InvalidOperationException("Battle scene is missing from the build scene list.");
            var operation = SceneManager.LoadSceneAsync(AppRoot.BattleScenePath, LoadSceneMode.Single);
            if (operation == null) throw new InvalidOperationException("Battle scene loading could not start.");
            if (operation.isDone) return Task.CompletedTask;
            var completion = new TaskCompletionSource<bool>();
            operation.completed += _ => completion.TrySetResult(true); return completion.Task;
        }
    }
}
