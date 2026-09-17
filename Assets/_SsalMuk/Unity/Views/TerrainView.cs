using System.Collections.Generic;
using SsalMuk.Core;
using UnityEngine;

namespace SsalMuk.Unity
{
    public sealed class TerrainView : MonoBehaviour
    {
        private Mesh mesh;
        private readonly List<SpriteRenderer> furniture = new List<SpriteRenderer>();
        public string Fingerprint { get; private set; }
        public void Initialize(ChunkData chunk, Material material, VisualCatalog visuals = null)
        {
            Fingerprint = chunk.Fingerprint;
            var vertices = new List<Vector3>(); var colors = new List<Color>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                Quad(x, y, 1, 1, (x + y) % 2 == 0 ? new Color32(28, 48, 42, 255) : new Color32(30, 51, 45, 255));
                if (!chunk.IsBlocked(x, y) || chunk.Furniture.Count > 0) continue;
                Quad(x + 0.05f, y - 0.07f, 0.92f, 0.94f, new Color32(12, 28, 24, 255));
                Quad(x + 0.035f, y + 0.025f, 0.93f, 0.94f, new Color32(69, 94, 81, 255));
                Quad(x + 0.07f, y + 0.83f, 0.86f, 0.09f, new Color32(100, 128, 107, 255));
            }
            mesh = new Mesh { name = "RuntimeChunk" }; mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
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
            SetRelativeOrigin(DVec2.Zero);
            void Quad(float x, float y, float width, float height, Color color)
            {
                int n = vertices.Count;
                vertices.Add(new Vector3(x, y, 0)); vertices.Add(new Vector3(x, y + height, 0)); vertices.Add(new Vector3(x + width, y + height, 0)); vertices.Add(new Vector3(x + width, y, 0));
                for (int i = 0; i < 4; i++) { colors.Add(color); uv.Add(new Vector2(0.5f, 0.5f)); }
                triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 2); triangles.Add(n); triangles.Add(n + 2); triangles.Add(n + 3);
            }
        }
        public void SetRelativeOrigin(DVec2 relativeOrigin)
        {
            transform.localPosition = WorldRenderOrigin.ToVector(relativeOrigin);
            foreach (var sprite in furniture)
                sprite.sortingOrder = 1000 - Mathf.RoundToInt((float)relativeOrigin.Y * 30 + sprite.transform.localPosition.y * 30);
        }
        private void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
