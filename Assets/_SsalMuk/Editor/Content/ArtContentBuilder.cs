using System;
using System.Collections.Generic;
using System.Linq;
using SsalMuk.Core;
using SsalMuk.Unity;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace SsalMuk.Editor.Content
{
    public static class ArtContentBuilder
    {
        private const string Root = "Assets/_SsalMuk/Content/";
        [MenuItem("SsalMuk/Apply Final Art")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Import art outside Play Mode.");
            var catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>("Assets/_SsalMuk/Resources/Bootstrap/GameCatalog.asset");
            Apply(catalog);
        }
        public static void Apply(GameCatalog catalog)
        {
            if (catalog == null) throw new ArgumentException("Build the game catalog first.");
            // Measured opaque bounds plus a small transparent margin; original PNG bytes stay unchanged.
            var units = Import(Root + "Art/Characters/UnitsAtlas.png", 128, new[] {
                Slice("Knight", 200, 730, 246, 415, new Vector2(0.5f, 0)),
                Slice("Slime", 776, 780, 305, 247, new Vector2(0.5f, 0)),
                Slice("Bat", 32, 211, 566, 232, new Vector2(0.5f, 0)),
                Slice("Ogre", 624, 61, 607, 547, new Vector2(0.5f, 0)) });
            var sword = Import(Root + "Sprites/Weapons/IronSword.png", 128, new[] { Slice("IronSword", 87, 248, 1596, 387, new Vector2(0.015f, 192f / 387)) })[0];
            var spear = Import(Root + "Sprites/Weapons/IronSpear.png", 128, new[] { Slice("IronSpear", 54, 280, 2066, 154, new Vector2(0.015f, 82f / 154)) })[0];
            var axe = Import(Root + "Sprites/Weapons/IronAxe.png", 128, new[] { Slice("IronAxe", 62, 205, 1427, 598, new Vector2(0.015f, 479f / 598)) })[0];
            var staff = Import(Root + "Sprites/Weapons/RubyStaff.png", 128, new[] { Slice("RubyStaff", 70, 271, 1858, 255, new Vector2(0.015f, 163f / 255)) })[0];
            var slash = Import(Root + "Art/ThirdParty/MetaShinryu/Arcing.png", 128, new[] { Slice("Slash", 0, 0, 489, 327, new Vector2(0.5f, 0)) })[0];
            var circle = Import(Root + "Art/ThirdParty/MetaShinryu/Circular.png", 128, new[] { Slice("Circular", 0, 0, 366, 367, new Vector2(0.5f, 0.5f)) })[0];
            var thrust = Import(Root + "Art/ThirdParty/MetaShinryu/LungeThrust.png", 128, new[] { Slice("Thrust", 0, 0, 860, 380, new Vector2(0.5f, 0.5f)) })[0];
            var fire = Import(Root + "Art/ThirdParty/Umplix/Fireball.png", 128, Grid("Fireball", 2, 2, 128, new Vector2(0.25f, 0.5f)));
            var explosion = Import(Root + "Art/ThirdParty/BenHickling/Explosion.png", 100, Grid("Explosion", 10, 5, 100, new Vector2(0.5f, 0.35f)));
            string path = Root + "Definitions/VisualCatalog.asset";
            var visuals = AssetDatabase.LoadAssetAtPath<VisualCatalog>(path);
            if (visuals == null) { visuals = ScriptableObject.CreateInstance<VisualCatalog>(); AssetDatabase.CreateAsset(visuals, path); }
            visuals.Configure(new[] {
                new VisualCatalog.UnitArt(UnitKind.Player, units[0], 1.05f), new VisualCatalog.UnitArt(UnitKind.Normal, units[1], 0.65f),
                new VisualCatalog.UnitArt(UnitKind.Air, units[2], 0.53f), new VisualCatalog.UnitArt(UnitKind.Boss, units[3], 2.25f) }, new[] {
                new VisualCatalog.WeaponArt(WeaponKind.Sword, sword, slash, 90), new VisualCatalog.WeaponArt(WeaponKind.Spear, spear, thrust, 24),
                new VisualCatalog.WeaponArt(WeaponKind.Axe, axe, circle, 0, 0.88f), new VisualCatalog.WeaponArt(WeaponKind.Fireball, staff, explosion[0], 0) }, fire, explosion);
            EditorUtility.SetDirty(visuals); AssetDatabase.SaveAssetIfDirty(visuals);
            catalog.ConfigureVisualCatalog(visuals); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
            string prefabPath = AssetDatabase.GetAssetPath(catalog.UnitViewPrefab);
            var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var view = prefab.GetComponent<UnitView>(); view.PrepareHierarchy();
                PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            catalog.ValidatePresentation();
        }
        private static SpriteRect Slice(string name, int x, int y, int width, int height, Vector2 pivot) =>
            new SpriteRect { name = name, rect = new Rect(x, y, width, height), pivot = pivot, alignment = SpriteAlignment.Custom };
        private static SpriteRect[] Grid(string name, int columns, int rows, int size, Vector2 pivot)
        {
            var result = new SpriteRect[columns * rows];
            for (int row = 0; row < rows; row++) for (int col = 0; col < columns; col++)
                result[row * columns + col] = Slice(name + "_" + (row * columns + col).ToString("D2"), col * size, (rows - 1 - row) * size, size, size, pivot);
            return result;
        }
        private static Sprite[] Import(string path, float ppu, SpriteRect[] rects)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing art source: " + path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = ppu; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
            importer.maxTextureSize = 4096;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false; importer.SetTextureSettings(settings);
            var factories = new SpriteDataProviderFactories(); factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
            var oldRects = provider.GetSpriteRects();
            foreach (var rect in rects) rect.spriteID = oldRects.FirstOrDefault(x => x.name == rect.name)?.spriteID ?? GUID.Generate();
            provider.SetSpriteRects(rects);
            var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            names.SetNameFileIdPairs(rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply(); importer.SaveAndReimport();
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(sprite => sprite.name);
            return rects.Select(rect => sprites[rect.name]).ToArray();
        }
    }
}
