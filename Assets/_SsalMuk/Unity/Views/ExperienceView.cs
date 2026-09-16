using SsalMuk.Core;
using UnityEngine;
namespace SsalMuk.Unity
{
    public sealed class ExperienceView : PooledView
    {
        private GameCatalog catalog;
        private ShapeRenderer core, halo, glint;
        private double radius = 0.08;
        public long ExperienceId { get; private set; }
        public ExperienceTier Tier { get; private set; }
        public Color DisplayColor => core == null ? Color.clear : core.DisplayColor;
        public float HaloRadius => catalog == null ? 0 : catalog.Visuals.ExperienceHaloRadius(Tier);
        public ExperienceState DisplayedState { get; private set; }
        public void Initialize(GameCatalog gameCatalog)
        {
            catalog = gameCatalog;
            halo = ShapeRenderer.Create(transform, "Glow", catalog.WorldMaterial, -100);
            core = ShapeRenderer.Create(transform, "Core", catalog.WorldMaterial, -99);
            glint = ShapeRenderer.Create(transform, "Glint", catalog.WorldMaterial, -98);
        }
        public void Bind(long id, ExperienceTier tier)
        {
            if (ExperienceId == id && Tier == tier) return;
            ExperienceId = id; Tier = tier; Draw();
        }
        private void Draw()
        {
            if (catalog == null) return;
            var color = catalog.Visuals.ExperienceColor(Tier); core.Disc(radius, color);
            color.a = 0.72f; halo.Disc(HaloRadius, color, true);
            glint.Disc(radius * 0.32, new Color(0.95f, 1, 0.95f, 0.9f));
            glint.transform.localPosition = new Vector3((float)-radius * 0.18f, (float)radius * 0.2f, 0);
        }
        public void Show(ExperienceRecord record, ExperienceTier tier, Vector3 position, double bodyRadius)
        {
            if (radius != bodyRadius) { radius = bodyRadius; ExperienceId = 0; }
            Bind(record.Id, tier); DisplayedState = record.State; SetPosition(position);
        }
        public void SetPosition(Vector3 position) => transform.localPosition = position;
        public bool TrySetPosition(LeaseToken token, Vector3 position)
        { if (!Accepts(token)) return false; SetPosition(position); return true; }
        public override void ResetVisuals()
        { base.ResetVisuals(); ExperienceId = 0; Tier = ExperienceTier.Green; radius = 0.08; DisplayedState = ExperienceState.Grounded; Draw(); }
    }
}
