using System;
using UnityEngine;
namespace SsalMuk.Unity
{
    public sealed class HitFeedback
    {
        private long sequence;
        private double hitAt = double.NegativeInfinity;
        public Color Color { get; private set; } = Color.white;
        public float Scale { get; private set; } = 1;
        public bool Observe(long nextSequence, double acceptedAt)
        {
            if (nextSequence <= sequence || double.IsNaN(acceptedAt) || double.IsInfinity(acceptedAt)) return false;
            sequence = nextSequence; hitAt = acceptedAt; return true;
        }
        public void Sample(double time, VisualCatalog catalog)
        {
            double elapsed = time - hitAt;
            Color = elapsed >= 0 && elapsed < catalog.HitRedSeconds ? Color.red : Color.white;
            Scale = elapsed >= 0 && elapsed < catalog.HitPopSeconds ?
                (float)(1 + (catalog.HitPopScale - 1) * (1 - elapsed / catalog.HitPopSeconds)) : 1;
        }
        public void Reset() { sequence = 0; hitAt = double.NegativeInfinity; Color = Color.white; Scale = 1; }
    }
}
