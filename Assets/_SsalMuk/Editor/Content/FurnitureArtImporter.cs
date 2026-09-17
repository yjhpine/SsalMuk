using System;
using System.Linq;
using SsalMuk.Unity;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace SsalMuk.Editor
{
    // Explicit one-shot import; never runs from an asset callback or changes scene contents.
    public static class FurnitureArtImporter
    {
        public const string Path = "Assets/_SsalMuk/Content/Art/Environment/AbandonedFurniture.png";
        public static void Apply()
        {
            AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(Path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Furniture atlas is missing.");
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 128; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
            importer.maxTextureSize = 2048; importer.isReadable = true;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings); importer.SaveAndReimport();
            try
            {
                var factories = new SpriteDataProviderFactories(); factories.Init();
                var provider = factories.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
                var edit = provider.GetDataProvider<ISpriteFrameEditCapability>();
                if (edit == null) throw new InvalidOperationException("Sprite edit capability is unavailable.");
                var capability = edit.GetEditCapability();
                foreach (var needed in new[] { EEditCapability.CreateAndDeleteSprite, EEditCapability.EditSpriteRect, EEditCapability.EditPivot, EEditCapability.EditSpriteName })
                    if (!capability.HasCapability(needed)) throw new InvalidOperationException("Sprite importer cannot perform " + needed);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Path);
                if (texture.width != 1536 || texture.height != 1024) throw new InvalidOperationException("Expected a 3 by 2 atlas of 512px cells.");
                var pixels = texture.GetPixels32(); var oldRects = provider.GetSpriteRects(); var rects = new SpriteRect[6];
                string[] kinds = { "Bookshelf", "Desk", "Chair" };
                for (int row = 0; row < 2; row++) for (int col = 0; col < 3; col++)
                {
                    int left = col * 512, bottom = (1 - row) * 512;
                    int minX = left + 512, maxX = left - 1, minY = bottom + 512, maxY = bottom - 1;
                    for (int y = bottom; y < bottom + 512; y++) for (int x = left; x < left + 512; x++)
                        if (pixels[y * texture.width + x].a > 8)
                        { minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y); }
                    if (maxX < minX) throw new InvalidOperationException("Empty furniture cell.");
                    minX = Math.Max(left, minX - 2); maxX = Math.Min(left + 511, maxX + 2);
                    minY = Math.Max(bottom, minY - 2); maxY = Math.Min(bottom + 511, maxY + 2);
                    string name = kinds[col] + "_" + row;
                    rects[row * 3 + col] = new SpriteRect { name = name, rect = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1),
                        alignment = SpriteAlignment.Custom, pivot = new Vector2(.5f, 0),
                        spriteID = oldRects.FirstOrDefault(x => x.name == name)?.spriteID ?? GUID.Generate() };
                }
                var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
                if (names == null) throw new InvalidOperationException("Stable sprite name mapping is unavailable.");
                provider.SetSpriteRects(rects); names.SetNameFileIdPairs(rects.Select(x => new SpriteNameFileIdPair(x.name, x.spriteID))); provider.Apply();
                importer.isReadable = false; importer.SaveAndReimport();
                var sprites = AssetDatabase.LoadAllAssetsAtPath(Path).OfType<Sprite>().ToDictionary(x => x.name);
                var catalog = AssetDatabase.LoadAssetAtPath<VisualCatalog>("Assets/_SsalMuk/Content/Definitions/VisualCatalog.asset");
                if (catalog == null) throw new InvalidOperationException("Visual catalog is missing.");
                catalog.ConfigureFurniture(rects.Select(x => sprites[x.name]).ToArray());
                EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
                Debug.Log("Imported six furniture sprites with bottom pivots and simple runtime footprints.");
            }
            finally { if (importer.isReadable) { importer.isReadable = false; importer.SaveAndReimport(); } }
        }
    }
}
