using System;
using SsalMuk.Core;
using UnityEngine;

namespace SsalMuk.Unity
{
    public sealed class PowerupView : PooledView
    {
        private ShapeRenderer halo, disc, rim;
        private readonly ShapeRenderer[] strokes = new ShapeRenderer[16];
        private PowerupKind? drawnKind;
        private int used;
        public long ItemId { get; private set; }
        public PowerupKind Kind { get; private set; }
        public void Initialize(GameCatalog catalog)
        {
            halo = ShapeRenderer.Create(transform, "ItemGlow", catalog.WorldMaterial, 40);
            disc = ShapeRenderer.Create(transform, "ItemDisc", catalog.WorldMaterial, 41);
            rim = ShapeRenderer.Create(transform, "ItemRim", catalog.WorldMaterial, 42);
            for (int i = 0; i < strokes.Length; i++) strokes[i] = ShapeRenderer.Create(transform, "Icon" + i, catalog.WorldMaterial, 43);
        }
        public void Show(PowerupRecord record, DVec2 relative)
        {
            ItemId = record.Id; Kind = record.Kind; transform.localPosition = WorldRenderOrigin.ToVector(relative);
            if (drawnKind == Kind) return;
            drawnKind = Kind; used = 0;
            foreach (var stroke in strokes) { stroke.Clear(); stroke.transform.localPosition = Vector3.zero; stroke.transform.localRotation = Quaternion.identity; }
            Color color = Kind == PowerupKind.Magnet ? new Color(.35f, 1, .65f) :
                Kind == PowerupKind.Invincibility ? new Color(.35f, .7f, 1) :
                Kind == PowerupKind.MoveSpeed ? new Color(1, .9f, .3f) : new Color(1, .45f, .3f);
            var glow = color; glow.a = .48f; halo.Disc(.55, glow, true);
            disc.Disc(.31, new Color(.04f, .07f, .1f, .95f)); rim.Ring(.285, .315, 0, Math.PI * 2, color);
            if (Kind == PowerupKind.Magnet)
            {
                Line(-.16, .15, -.16, -.09); Line(-.16, -.09, -.1, -.17); Line(-.1, -.17, .1, -.17);
                Line(.1, -.17, .16, -.09); Line(.16, -.09, .16, .15);
            }
            else if (Kind == PowerupKind.Invincibility)
            {
                Line(-.17, .14, 0, .2); Line(0, .2, .17, .14); Line(.17, .14, .13, -.1);
                Line(.13, -.1, 0, -.2); Line(0, -.2, -.13, -.1); Line(-.13, -.1, -.17, .14);
            }
            else if (Kind == PowerupKind.MoveSpeed)
            {
                Line(.04, .22, -.13, -.02); Line(-.13, -.02, .08, -.02); Line(.08, -.02, -.06, -.22);
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    double a = i * Math.PI / 2; var d = new DVec2(Math.Cos(a), Math.Sin(a)); var side = new DVec2(-d.Y, d.X);
                    var tip = d * .22; var basePoint = d * .12;
                    Segment(d * .04, tip); Segment(tip, basePoint + side * .06); Segment(tip, basePoint - side * .06);
                }
            }
        }
        private void Line(double x, double y, double x2, double y2) => Segment(new DVec2(x, y), new DVec2(x2, y2));
        private void Segment(DVec2 a, DVec2 b)
        {
            var stroke = strokes[used++]; var delta = b - a;
            stroke.Capsule(delta.Length, .023, new Color(.97f, 1, 1));
            stroke.transform.localPosition = WorldRenderOrigin.ToVector(a);
            stroke.transform.localRotation = Quaternion.Euler(0, 0, (float)(Math.Atan2(delta.Y, delta.X) * 180 / Math.PI));
        }
        public override void ResetVisuals() { base.ResetVisuals(); ItemId = 0; drawnKind = null; }
    }
}
