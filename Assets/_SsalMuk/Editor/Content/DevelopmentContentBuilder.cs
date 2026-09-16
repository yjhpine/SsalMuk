using System;
using System.Collections.Generic;
using System.IO;
using SsalMuk.Core;
using SsalMuk.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SsalMuk.Editor.Content
{
    public static class DevelopmentContentBuilder
    {
        private const string Root = "Assets/_SsalMuk/";
        [MenuItem("SsalMuk/Build Development Content")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Create development content outside Play Mode.");
            EnsureFolder(Root + "Content/Definitions"); EnsureFolder(Root + "Content/Prefabs");
            EnsureFolder(Root + "Content/Sprites"); EnsureFolder(Root + "Content/Materials");
            EnsureFolder(Root + "Resources/Bootstrap"); EnsureFolder(Root + "Scenes");
            var font = AssetDatabase.LoadAssetAtPath<Font>(Root + "Content/Fonts/GowunDodum-Regular.ttf");
            if (font == null) throw new InvalidOperationException("The licensed Korean font must be imported first.");
            Sprite body = SpriteAsset("DevelopmentBody", true); Sprite solid = SpriteAsset("DevelopmentSolid", false);
            var defaults = AssetDatabase.LoadAssetAtPath<DevelopmentDefaults>(Root + "Content/Definitions/DevelopmentDefaults.asset");
            if (defaults == null) { defaults = ScriptableObject.CreateInstance<DevelopmentDefaults>(); AssetDatabase.CreateAsset(defaults, Root + "Content/Definitions/DevelopmentDefaults.asset"); }
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "Content/Materials/DevelopmentWorld.mat");
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) throw new InvalidOperationException("The project's URP 2D unlit sprite shader is missing.");
                material = new Material(shader) { name = "DevelopmentWorld", mainTexture = solid.texture };
                AssetDatabase.CreateAsset(material, Root + "Content/Materials/DevelopmentWorld.mat");
            }
            var prefab = UnitPrefab(body, material);
            string catalogPath = Root + "Resources/Bootstrap/GameCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>(catalogPath);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<GameCatalog>(); AssetDatabase.CreateAsset(catalog, catalogPath); }
            var visuals = new List<GameCatalog.UnitVisual>();
            foreach (UnitKind kind in Enum.GetValues(typeof(UnitKind))) visuals.Add(new GameCatalog.UnitVisual(kind, body));
            catalog.Configure(defaults, prefab, visuals); catalog.ConfigurePresentation(font, solid, material); catalog.ValidatePresentation();
            EditorUtility.SetDirty(catalog);
            EmptyScene(AppRoot.MainMenuScenePath); EmptyScene(AppRoot.BattleScenePath);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(scene => scene.path == AppRoot.MainMenuScenePath)) scenes.Insert(0, new EditorBuildSettingsScene(AppRoot.MainMenuScenePath, true));
            if (!scenes.Exists(scene => scene.path == AppRoot.BattleScenePath)) scenes.Add(new EditorBuildSettingsScene(AppRoot.BattleScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        private static Sprite SpriteAsset(string name, bool circle)
        {
            string path = Root + "Content/Sprites/" + name + ".png";
            if (!File.Exists(path))
            {
                var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false); var pixels = new Color32[1024];
                for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                {
                    float dx = x - 15.5f, dy = y - 15.5f;
                    pixels[y * 32 + x] = new Color32(255, 255, 255, (byte)(!circle || dx * dx + dy * dy <= 225 ? 255 : 0));
                }
                texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single; importer.spritePixelsPerUnit = 32;
                importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spritePivot = circle ? new Vector2(0.5f, 0) : new Vector2(0.5f, 0.5f);
                importer.SetTextureSettings(settings); importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new InvalidOperationException("Sprite import failed: " + path);
        }
        private static GameObject UnitPrefab(Sprite sprite, Material material)
        {
            string path = Root + "Content/Prefabs/UnitView.prefab"; var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            Scene previous = SceneManager.GetActiveScene(); Scene temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(temporary);
                var root = new GameObject("UnitView"); var view = root.AddComponent<UnitView>();
                var body = new GameObject("VisualRoot", typeof(SpriteRenderer)); body.transform.SetParent(root.transform, false);
                var shadow = new GameObject("Shadow", typeof(SpriteRenderer)); shadow.transform.SetParent(root.transform, false);
                var bodyRenderer = body.GetComponent<SpriteRenderer>(); bodyRenderer.sprite = sprite; bodyRenderer.sharedMaterial = material;
                var shadowRenderer = shadow.GetComponent<SpriteRenderer>(); shadowRenderer.sprite = sprite; shadowRenderer.sharedMaterial = material;
                view.Configure(bodyRenderer, shadowRenderer);
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { EditorSceneManager.CloseScene(temporary, true); if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
        }
        private static void EmptyScene(string path)
        {
            if (File.Exists(path)) return;
            Scene previous = SceneManager.GetActiveScene(); Scene created = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try { if (!EditorSceneManager.SaveScene(created, path)) throw new InvalidOperationException("Scene could not be saved: " + path); }
            finally { EditorSceneManager.CloseScene(created, true); if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
        }
    }
}
