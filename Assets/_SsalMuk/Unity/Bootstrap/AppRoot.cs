using SsalMuk.Core;
using SsalMuk.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace SsalMuk.Unity
{
    public sealed class AppRoot : MonoBehaviour
    {
        public const string MainMenuScenePath = "Assets/_SsalMuk/Scenes/MainMenu.unity";
        public const string BattleScenePath = "Assets/_SsalMuk/Scenes/Battle.unity";
        private static AppRoot instance;
        private MainMenuPresenter presenter;
        public static AppRoot Instance => instance;
        public RunCoordinator Coordinator { get; private set; }
        public MainMenuView Menu { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { SceneManager.sceneLoaded -= SceneLoaded; instance = null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install() { SceneManager.sceneLoaded -= SceneLoaded; SceneManager.sceneLoaded += SceneLoaded; }
        private static void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != MainMenuScenePath && scene.path != BattleScenePath) return;
            if (instance == null)
            {
                var existing = FindAnyObjectByType<AppRoot>();
                if (existing != null) instance = existing;
                else new GameObject("SsalMuk App").AddComponent<AppRoot>();
            }
            if (scene.path == BattleScenePath && instance != null && instance.Coordinator.Phase == RunPhase.MainMenu)
                _ = instance.Coordinator.StartRunAsync();
        }
        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this; DontDestroyOnLoad(gameObject);
            var catalog = Resources.Load<GameCatalog>("Bootstrap/GameCatalog");
            var cameraObject = new GameObject("RuntimeCamera", typeof(Camera)); cameraObject.transform.SetParent(transform, false);
            cameraObject.tag = "MainCamera"; cameraObject.transform.localPosition = new Vector3(0, 0, -10);
            var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 10;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = UiFactory.Ink;
            var lightObject = new GameObject("RuntimeLight", typeof(Light)); lightObject.transform.SetParent(transform, false);
            lightObject.GetComponent<Light>().type = LightType.Directional;
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("RuntimeEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false); events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            var menuObject = new GameObject("MainMenu"); menuObject.transform.SetParent(transform, false);
            Menu = menuObject.AddComponent<MainMenuView>();
            Menu.Initialize(catalog != null && catalog.UiFont != null ? catalog.UiFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            Coordinator = new RunCoordinator(new UnitySceneLoader(), () =>
            {
                if (catalog == null) throw new System.InvalidOperationException("Game catalog is missing.");
                return new RunScope(catalog);
            });
            presenter = new MainMenuPresenter(Coordinator, Menu);
        }
        private void OnDestroy()
        {
            presenter?.Dispose(); Coordinator?.Dispose(); if (instance == this) instance = null;
        }
    }
}
