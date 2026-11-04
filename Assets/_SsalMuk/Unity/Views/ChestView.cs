using System.Collections.Generic;
using SsalMuk.Core;
using UnityEngine;

namespace SsalMuk.Unity
{
    public sealed class ChestView : PooledView
    {
        private Mesh mesh;
        private MeshRenderer rendererComponent;
        private List<Color> vertexColors;
        private ItemRarity? shownRarity;
        public long ChestId { get; private set; }
        public void Initialize(GameCatalog catalog)
        {
            mesh = new Mesh { name = "Chest" };
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            rendererComponent = gameObject.AddComponent<MeshRenderer>(); rendererComponent.sharedMaterial = catalog.WorldMaterial;
            // The sprite shader expects per-renderer tint/flip values even on a MeshRenderer.
            // Supply them explicitly so a preceding SpriteRenderer cannot affect this mesh.
            var properties = new MaterialPropertyBlock();
            properties.SetColor("unity_SpriteColor", Color.white);
            properties.SetVector("unity_SpriteProps", new Vector4(1, 1, -1, 0));
            rendererComponent.SetPropertyBlock(properties);
            var vertices = new List<Vector3>(); var colors = new List<Color>(); var uv = new List<Vector2>(); var indices = new List<int>();
            vertexColors = colors;
            var edge = new Color32(42, 25, 20, 255); var wood = new Color32(131, 75, 39, 255); var gold = new Color32(238, 191, 83, 255);
            Quad(-.65f, -.3f, 1.3f, .98f, edge);
            Quad(-.59f, -.24f, 1.18f, .55f, wood);
            Quad(-.57f, .34f, 1.14f, .27f, new Color32(178, 111, 52, 255));
            Quad(-.59f, .27f, 1.18f, .075f, gold);
            Quad(-.46f, -.24f, .09f, .86f, gold); Quad(.37f, -.24f, .09f, .86f, gold);
            Quad(-.13f, .09f, .26f, .3f, edge); Quad(-.09f, .13f, .18f, .24f, gold);
            Quad(-.025f, .18f, .05f, .1f, edge);
            Quad(-.34f, -.13f, .66f, .035f, new Color32(91, 48, 29, 255));
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, uv); mesh.SetTriangles(indices, 0); mesh.RecalculateBounds();
            void Quad(float x, float y, float width, float height, Color color)
            {
                int n = vertices.Count;
                vertices.Add(new Vector3(x, y)); vertices.Add(new Vector3(x + width, y)); vertices.Add(new Vector3(x + width, y + height)); vertices.Add(new Vector3(x, y + height));
                for (int i = 0; i < 4; i++) { colors.Add(color); uv.Add(new Vector2(.5f, .5f)); }
                indices.AddRange(new[] { n, n + 2, n + 1, n, n + 3, n + 2 });
            }
        }
        public void Show(ChestRecord chest, DVec2 relative)
        {
            if (shownRarity != chest.Rarity)
            {
                shownRarity = chest.Rarity; var color = ItemUiStyle.Color(chest.Rarity);
                for (int i = 12; i < 24; i++) vertexColors[i] = color;
                for (int i = 28; i < 32; i++) vertexColors[i] = color;
                mesh.SetColors(vertexColors);
            }
            ChestId = chest.Id; transform.localPosition = WorldRenderOrigin.ToVector(relative);
            rendererComponent.sortingOrder = 1000 - Mathf.RoundToInt(((float)relative.Y - .3f) * 30);
        }
        public override void ResetVisuals() { base.ResetVisuals(); ChestId = 0; }
        private void OnDestroy() { if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); } }
    }
}
