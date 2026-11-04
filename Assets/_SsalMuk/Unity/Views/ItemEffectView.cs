using UnityEngine;
using SsalMuk.Core;
namespace SsalMuk.Unity
{
    public sealed class ItemEffectView : PooledView
    {
        private LineRenderer line;
        private ShapeRenderer shape;
        public void Initialize(GameCatalog catalog)
        {
            line = gameObject.AddComponent<LineRenderer>(); line.sharedMaterial = catalog.WorldMaterial; line.useWorldSpace = false;
            line.widthMultiplier = .075f; line.numCapVertices = 2; line.sortingOrder = 4500;
            shape = ShapeRenderer.Create(transform, "Pulse", catalog.WorldMaterial, 4500);
            var properties = new MaterialPropertyBlock(); properties.SetColor("unity_SpriteColor", Color.white);
            properties.SetVector("unity_SpriteProps", new Vector4(1,1,-1,0));
            line.SetPropertyBlock(properties); shape.GetComponent<MeshRenderer>().SetPropertyBlock(properties);
        }
        public void Show(ItemEffectVisual effect, DVec2 relative, double now)
        {
            transform.localPosition = WorldRenderOrigin.ToVector(relative);
            float fade = Mathf.Clamp01(1 - (float)((now - effect.StartedAt) / ItemEffectVisual.Duration));
            line.enabled = effect.Kind == ItemEffectKind.Lightning; shape.gameObject.SetActive(!line.enabled);
            if (line.enabled)
            {
                line.positionCount = effect.Points.Count;
                for (int i = 0; i < effect.Points.Count; i++) line.SetPosition(i, WorldRenderOrigin.ToVector(effect.Position.DisplacementTo(effect.Points[i])));
                line.startColor = line.endColor = new Color(.5f,.82f,1,fade);
            }
            else
            {
                Color color = effect.Kind == ItemEffectKind.Explosion ? new Color(1,.4f,.1f,.5f * fade) :
                    effect.Kind == ItemEffectKind.Shield ? new Color(.4f,.72f,1,.8f * fade) : new Color(.4f,1,.65f,.8f * fade);
                if (effect.Kind == ItemEffectKind.Explosion) shape.Disc(effect.Radius, color, true);
                else shape.Ring(effect.Radius * .85, effect.Radius, 0, System.Math.PI * 2, color);
            }
        }
        public override void ResetVisuals() { base.ResetVisuals(); if (line != null) { line.positionCount = 0; line.enabled = false; } shape?.Clear(); }
    }
}
