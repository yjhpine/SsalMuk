using System;
using System.Collections.Generic;
using SsalMuk.Core;

namespace SsalMuk.Presentation
{
    public sealed class WorldPresenter
    {
        private readonly IWorldView view;
        private readonly HashSet<long> visible = new HashSet<long>();
        private Guid runId;
        public WorldPresenter(IWorldView view) { this.view = view ?? throw new ArgumentNullException(nameof(view)); }
        public void Refresh(IRunReadModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (model.RunId != runId) { visible.Clear(); runId = model.RunId; }
            var nextVisible = new HashSet<long>(); view.BeginFrame(runId); var origin = model.ViewOrigin;
            foreach (var chunk in model.Terrain)
            {
                var relative = origin.DisplacementTo(new WorldPosition(chunk.Coord, DVec2.Zero));
                if (relative.X > 36 || relative.X + 32 < -36 || relative.Y > 36 || relative.Y + 32 < -36) continue;
                view.ShowTerrain(chunk, relative);
            }
            int enemies = 0;
            foreach (var unit in model.Units)
            {
                if (!unit.IsAlive) continue;
                if (unit.Kind != UnitKind.Player) enemies++;
                var relative = origin.DisplacementTo(unit.Position);
                if (relative.Length > (visible.Contains(unit.Id) ? 24 : 22)) continue;
                nextVisible.Add(unit.Id); view.ShowUnit(unit, relative);
            }
            visible.Clear(); visible.UnionWith(nextVisible); view.EndFrame(model.Clock.ElapsedSeconds, enemies);
        }
    }
}
