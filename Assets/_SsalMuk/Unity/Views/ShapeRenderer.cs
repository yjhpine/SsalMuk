using System;
using System.Collections.Generic;
using SsalMuk.Core;
using UnityEngine;
namespace SsalMuk.Unity
{
    public sealed class ShapeRenderer : MonoBehaviour
    {
        private readonly List<Vector3> vertices = new List<Vector3>(132);
        private readonly List<Color> colors = new List<Color>(132);
        private readonly List<Vector2> uv = new List<Vector2>(132);
        private readonly List<int> triangles = new List<int>(384);
        private Mesh mesh;
        private MeshRenderer meshRenderer;
        public Bounds LocalBounds => mesh == null ? default : mesh.bounds;
        public Color DisplayColor { get; private set; }
        public static ShapeRenderer Create(Transform parent, string name, Material material, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); var shape = go.AddComponent<ShapeRenderer>();
            shape.mesh = new Mesh { name = name + "Mesh" }; shape.mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = shape.mesh;
            shape.meshRenderer = go.AddComponent<MeshRenderer>(); shape.meshRenderer.sharedMaterial = material; shape.meshRenderer.sortingOrder = order;
            return shape;
        }
        public void SetOrder(int order) => meshRenderer.sortingOrder = order;
        public void Disc(double radius, Color color, bool soft = false)
        {
            Begin(color); Add(DVec2.Zero, color);
            for (int i = 0; i <= 48; i++)
            {
                double angle = i * Math.PI * 2 / 48; var edge = color; if (soft) edge.a = 0;
                Add(new DVec2(Math.Cos(angle), Math.Sin(angle)) * radius, edge);
                if (i > 0) Triangle(0, i, i + 1);
            }
            Apply();
        }
        public void Ring(double inner, double outer, double startAngle, double endAngle, Color color)
        {
            Begin(color);
            for (int i = 0; i <= 64; i++)
            {
                double angle = startAngle + (endAngle - startAngle) * i / 64; var direction = new DVec2(Math.Cos(angle), Math.Sin(angle));
                Add(direction * inner, color); Add(direction * outer, color);
                if (i > 0) { int n = i * 2; Triangle(n - 2, n - 1, n + 1); Triangle(n - 2, n + 1, n); }
            }
            Apply();
        }
        public void Capsule(double length, double radius, Color color)
        {
            Begin(color); Add(new DVec2(length / 2, 0), color);
            for (int i = 0; i <= 24; i++)
            {
                double angle = -Math.PI / 2 + Math.PI * i / 24;
                Add(new DVec2(length + radius * Math.Cos(angle), radius * Math.Sin(angle)), color);
            }
            for (int i = 0; i <= 24; i++)
            {
                double angle = Math.PI / 2 + Math.PI * i / 24;
                Add(new DVec2(radius * Math.Cos(angle), radius * Math.Sin(angle)), color);
            }
            for (int i = 1; i < vertices.Count - 1; i++) Triangle(0, i, i + 1);
            Triangle(0, vertices.Count - 1, 1); Apply();
        }
        public void Clear() { if (mesh != null) mesh.Clear(); }
        private void Begin(Color color) { DisplayColor = color; vertices.Clear(); colors.Clear(); uv.Clear(); triangles.Clear(); }
        private void Add(DVec2 position, Color color) { vertices.Add(WorldRenderOrigin.ToVector(position)); colors.Add(color); uv.Add(new Vector2(0.5f, 0.5f)); }
        private void Triangle(int a, int b, int c) { triangles.Add(a); triangles.Add(b); triangles.Add(c); }
        private void Apply()
        { mesh.Clear(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds(); }
        private void OnDestroy() { if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); } }
    }
}
