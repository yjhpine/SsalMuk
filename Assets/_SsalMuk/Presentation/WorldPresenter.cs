using System;
using System.Collections.Generic;
using SsalMuk.Core;

namespace SsalMuk.Presentation
{
    public sealed class WorldPresenter
    {
        private readonly IWorldView view;
        private readonly HashSet<long> visible = new HashSet<long>(), visibleExperience = new HashSet<long>();
        private readonly HashSet<long> nextVisible = new HashSet<long>(), nextExperience = new HashSet<long>();
        private Guid runId;
        public WorldPresenter(IWorldView view) { this.view = view ?? throw new ArgumentNullException(nameof(view)); }
        public void Refresh(IRunReadModel model, double interpolation = 1, double viewRadius = 22)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (interpolation < 0 || interpolation > 1 || double.IsNaN(interpolation) || viewRadius <= 0) throw new ArgumentOutOfRangeException(nameof(interpolation));
            if (model.RunId != runId) { visible.Clear(); visibleExperience.Clear(); runId = model.RunId; }
            nextVisible.Clear(); nextExperience.Clear(); view.BeginFrame(runId); var origin = model.ViewOrigin;
            foreach (var unit in model.Units)
                if (unit.Kind == UnitKind.Player) { origin = Interpolate(unit, interpolation); break; }
            var combatView = view as ICombatWorldView;
            double renderTime = Math.Max(0, model.Clock.ElapsedSeconds - model.Clock.FixedStep * (1 - interpolation));
            combatView?.SetFrameTime(renderTime);
            foreach (var chunk in model.Terrain)
            {
                var relative = origin.DisplacementTo(new WorldPosition(chunk.Coord, DVec2.Zero));
                if (relative.X > viewRadius + 14 || relative.X + 32 < -viewRadius - 14 || relative.Y > viewRadius + 14 || relative.Y + 32 < -viewRadius - 14) continue;
                view.ShowTerrain(chunk, relative);
            }
            int enemies = 0;
            foreach (var unit in model.Units)
            {
                if (!unit.IsAlive) continue;
                if (unit.Kind != UnitKind.Player) enemies++;
                var relative = origin.DisplacementTo(Interpolate(unit, interpolation));
                bool telegraph = unit is GroundEnemyModel ground && ground.Charge?.Phase == EnemyState.Telegraph;
                if (!telegraph && relative.Length > (visible.Contains(unit.Id) ? viewRadius + 2 : viewRadius)) continue;
                nextVisible.Add(unit.Id); view.ShowUnit(unit, relative);
            }
            if (combatView != null)
            {
                foreach (var record in model.Experience)
                {
                    if (record.State == ExperienceState.Collected) continue;
                    var position = record.State == ExperienceState.Attracting ? record.PreviousPosition.Offset(record.PreviousPosition.DisplacementTo(record.Position) * interpolation) : record.Position;
                    var relative = origin.DisplacementTo(position);
                    if (relative.Length > (visibleExperience.Contains(record.Id) ? viewRadius + 2 : viewRadius)) continue;
                    nextExperience.Add(record.Id);
                    combatView.ShowExperience(record, model.PickupSettings.TierFor(record.Value), relative, model.PickupSettings.OrbRadius);
                }
                if (model.Combat != null)
                {
                    foreach (var shape in model.Combat.AttackShapes)
                    {
                        var relative = origin.DisplacementTo(shape.Origin);
                        if (relative.Length <= viewRadius + shape.Range + shape.Width) combatView.ShowAttack(shape, relative);
                    }
                    foreach (var shot in model.Combat.Projectiles)
                    {
                        var position = shot.PreviousPosition.Offset(shot.PreviousPosition.DisplacementTo(shot.Position) * interpolation);
                        var relative = origin.DisplacementTo(position);
                        if (relative.Length <= viewRadius + 2) combatView.ShowProjectile(shot, relative);
                    }
                    foreach (var explosion in model.Combat.Explosions)
                    {
                        var relative = origin.DisplacementTo(explosion.Position);
                        if (relative.Length <= viewRadius + explosion.Radius) combatView.ShowExplosion(explosion, relative, explosion.Duration);
                    }
                }
            }
            visible.Clear(); visible.UnionWith(nextVisible); visibleExperience.Clear(); visibleExperience.UnionWith(nextExperience);
            view.EndFrame(model.Clock.ElapsedSeconds, enemies);
        }
        private static WorldPosition Interpolate(UnitModel unit, double alpha)
            => alpha >= 1 ? unit.Position : alpha <= 0 ? unit.PreviousPosition :
                unit.PreviousPosition.Offset(unit.PreviousPosition.DisplacementTo(unit.Position) * alpha);
    }
}
