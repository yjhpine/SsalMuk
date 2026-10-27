using System.Collections.Generic;
using SsalMuk.Core;
using UnityEngine;

namespace SsalMuk.Unity
{
    public sealed class TerrainView : MonoBehaviour
    {
        private Mesh mesh, grassMesh;
        private MeshRenderer grassRenderer;
        private MaterialPropertyBlock grassProperties;
        private Texture grassTexture;
        private Color[] normalColors, darkColors;
        private bool darkMode;
        private readonly List<SpriteRenderer> furniture = new List<SpriteRenderer>();
        public string Fingerprint { get; private set; }
        public void Initialize(ChunkData chunk, Material material, VisualCatalog visuals = null)
        {
            Fingerprint = chunk.Fingerprint;
            var vertices = new List<Vector3>(); var colors = new List<Color>(); var night = new List<Color>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                bool library = chunk.Map == MapKind.Library;
                if (library && (y < LibraryChunkGenerator.InteriorBottom || y >= LibraryChunkGenerator.InteriorTop)) continue;
                var floor = library ? ((x / 4 + y) % 2 == 0 ? new Color32(60, 47, 34, 255) : new Color32(54, 42, 30, 255)) :
                    ((x + y) % 2 == 0 ? new Color32(28, 48, 42, 255) : new Color32(30, 51, 45, 255));
                Quad(x, y, 1, 1, floor);
                if (chunk.Map == MapKind.Forest) continue;
                if (!chunk.IsBlocked(x, y) || chunk.Furniture.Count > 0) continue;
                Quad(x + 0.05f, y - 0.07f, 0.92f, 0.94f, new Color32(12, 28, 24, 255));
                Quad(x + 0.035f, y + 0.025f, 0.93f, 0.94f, new Color32(69, 94, 81, 255));
                Quad(x + 0.07f, y + 0.83f, 0.86f, 0.09f, new Color32(100, 128, 107, 255));
            }
            if (chunk.Map == MapKind.Library) LibraryWallArt.Draw(chunk.Coord.X, Quad);
            mesh = new Mesh { name = "RuntimeChunk" }; mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            normalColors = colors.ToArray(); darkColors = night.ToArray();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.sortingOrder = -1000;
            foreach (var placement in chunk.Furniture)
            {
                if (visuals == null) throw new System.ArgumentNullException(nameof(visuals), "Generated furniture needs its visual catalog.");
                var go = new GameObject(placement.Kind + " " + placement.X + "," + placement.Y);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(placement.X + placement.Width * .5f, placement.Y + .08f, 0);
                var sprite = go.AddComponent<SpriteRenderer>(); sprite.sprite = visuals.Furniture(placement.Kind, placement.Variation);
                sprite.sharedMaterial = material;
                go.transform.localScale = Vector3.one * ((placement.Width + .1f) / sprite.sprite.bounds.size.x);
                furniture.Add(sprite);
            }
            if (chunk.Scenery.Count > 0)
            {
                if (visuals == null) throw new System.ArgumentNullException(nameof(visuals));
                foreach (var prop in chunk.Scenery)
                {
                    if (!prop.BlocksMovement) continue;
                    var go = new GameObject(prop.Kind + " " + prop.X + "," + prop.Y); go.transform.SetParent(transform, false);
                    go.transform.localPosition = new Vector3(prop.X + 1, prop.Y + .12f, 0);
                    var sprite = go.AddComponent<SpriteRenderer>(); sprite.sprite = visuals.Forest(prop.Kind, prop.Variation); sprite.sharedMaterial = material;
                    float width = prop.Kind == ForestPropKind.Tree ? 2.7f : 2.1f;
                    go.transform.localScale = Vector3.one * (width / sprite.sprite.bounds.size.x); furniture.Add(sprite);
                }
                BuildGrass(chunk, material, visuals);
            }
            SetRelativeOrigin(DVec2.Zero);
            void Quad(float x, float y, float width, float height, Color color, float dim = .12f)
            {
                int n = vertices.Count;
                vertices.Add(new Vector3(x, y, 0)); vertices.Add(new Vector3(x, y + height, 0)); vertices.Add(new Vector3(x + width, y + height, 0)); vertices.Add(new Vector3(x + width, y, 0));
                for (int i = 0; i < 4; i++) { colors.Add(color); night.Add(new Color(color.r * dim, color.g * dim, color.b * dim, color.a)); uv.Add(new Vector2(0.5f, 0.5f)); }
                triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 2); triangles.Add(n); triangles.Add(n + 2); triangles.Add(n + 3);
            }
        }
        private void BuildGrass(ChunkData chunk, Material material, VisualCatalog visuals)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var colors = new List<Color>(); var triangles = new List<int>();
            foreach (var prop in chunk.Scenery)
            {
                if (prop.Kind != ForestPropKind.Grass) continue;
                var sprite = visuals.Forest(prop.Kind, prop.Variation); grassTexture = sprite.texture;
                float width = .8f + .2f * prop.Variation, height = width * sprite.rect.height / sprite.rect.width;
                float x = prop.X + .5f - width / 2, y = prop.Y + .1f; int n = vertices.Count;
                vertices.Add(new Vector3(x, y)); vertices.Add(new Vector3(x, y + height)); vertices.Add(new Vector3(x + width, y + height)); vertices.Add(new Vector3(x + width, y));
                var rect = sprite.rect; float tw = sprite.texture.width, th = sprite.texture.height;
                uv.Add(new Vector2(rect.xMin / tw, rect.yMin / th)); uv.Add(new Vector2(rect.xMin / tw, rect.yMax / th));
                uv.Add(new Vector2(rect.xMax / tw, rect.yMax / th)); uv.Add(new Vector2(rect.xMax / tw, rect.yMin / th));
                for (int i = 0; i < 4; i++) colors.Add(new Color(.65f, .7f, .6f, .85f));
                triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 2); triangles.Add(n); triangles.Add(n + 2); triangles.Add(n + 3);
            }
            if (vertices.Count == 0) return;
            var go = new GameObject("GrassBatch"); go.transform.SetParent(transform, false);
            grassMesh = new Mesh { name = "GrassChunk" }; grassMesh.SetVertices(vertices); grassMesh.SetUVs(0, uv); grassMesh.SetColors(colors); grassMesh.SetTriangles(triangles, 0); grassMesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = grassMesh;
            grassRenderer = go.AddComponent<MeshRenderer>(); grassRenderer.sharedMaterial = material; grassRenderer.sortingOrder = -900;
            grassProperties = new MaterialPropertyBlock(); ApplyGrassColor(false);
        }
        private void ApplyGrassColor(bool dark)
        {
            if (grassRenderer == null) return;
            grassProperties.SetTexture("_MainTex", grassTexture);
            grassProperties.SetColor("_Color", dark ? new Color(.35f, .35f, .35f, 1) : Color.white);
            grassRenderer.SetPropertyBlock(grassProperties);
        }
        public void SetRelativeOrigin(DVec2 relativeOrigin)
        {
            transform.localPosition = WorldRenderOrigin.ToVector(relativeOrigin);
            foreach (var sprite in furniture)
                sprite.sortingOrder = 1000 - Mathf.RoundToInt((float)relativeOrigin.Y * 30 + sprite.transform.localPosition.y * 30);
        }
        public void SetDarkMode(bool enabled)
        {
            if (mesh == null || darkMode == enabled) return;
            darkMode = enabled; mesh.colors = enabled ? darkColors : normalColors;
            ApplyGrassColor(enabled);
        }
        private void OnDestroy() { if (mesh != null) Destroy(mesh); if (grassMesh != null) Destroy(grassMesh); }
    }
}
