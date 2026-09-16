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
        private HudPresenter hudPresenter;
        private ResultsPresenter resultsPresenter;
        private LevelUpPresenter levelUpPresenter;
        public static AppRoot Instance => instance;
        public RunCoordinator Coordinator { get; private set; }
        public MainMenuView Menu { get; private set; }
        public HudView Hud { get; private set; }
        public ResultsView Results { get; private set; }
        public LevelUpView LevelUp { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { SceneManager.sceneLoaded -= SceneLoaded; instance = null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install() { SceneManager.sceneLoaded -= SceneLoaded; SceneManager.sceneLoaded += SceneLoaded; }
        private static void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != MainMenuScenePath && scene.path != BattleScenePath) return;
            bool created = instance == null;
            if (created)
            {
                var existing = FindAnyObjectByType<AppRoot>();
                if (existing != null) instance = existing;
                else new GameObject("SsalMuk App").AddComponent<AppRoot>();
            }
            if (created && scene.path == BattleScenePath && instance != null && instance.Coordinator.Phase == RunPhase.MainMenu)
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
            var font = catalog != null && catalog.UiFont != null ? catalog.UiFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Menu.Initialize(font);
            var hudObject = new GameObject("Hud"); hudObject.transform.SetParent(transform, false); Hud = hudObject.AddComponent<HudView>(); Hud.Initialize(font);
            var resultsObject = new GameObject("Results"); resultsObject.transform.SetParent(transform, false); Results = resultsObject.AddComponent<ResultsView>(); Results.Initialize(font);
            var levelUpObject = new GameObject("LevelUp"); levelUpObject.transform.SetParent(transform, false); LevelUp = levelUpObject.AddComponent<LevelUpView>(); LevelUp.Initialize(font);
            Coordinator = new RunCoordinator(new UnitySceneLoader(), () =>
            {
                if (catalog == null) throw new System.InvalidOperationException("Game catalog is missing.");
                return new RunScope(catalog);
            });
            presenter = new MainMenuPresenter(Coordinator, Menu);
            hudPresenter = new HudPresenter(Hud); resultsPresenter = new ResultsPresenter(Coordinator, Results);
            levelUpPresenter = new LevelUpPresenter(LevelUp);
        }
        private void LateUpdate() { if (Coordinator != null) { hudPresenter.Refresh(Coordinator.Run); levelUpPresenter.Refresh(Coordinator.Run); } }
        private void OnDestroy()
        {
            presenter?.Dispose(); resultsPresenter?.Dispose(); levelUpPresenter?.Dispose(); Coordinator?.Dispose(); if (instance == this) instance = null;
        }
    }
}
