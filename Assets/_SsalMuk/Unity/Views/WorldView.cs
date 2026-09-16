using System;
using System.Collections.Generic;
using SsalMuk.Core;
using SsalMuk.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace SsalMuk.Unity
{
    public sealed class WorldView : MonoBehaviour, IWorldView
    {
        private GameCatalog catalog;
        private readonly Dictionary<long, UnitView> units = new Dictionary<long, UnitView>();
        private readonly Dictionary<ChunkCoord, TerrainView> terrain = new Dictionary<ChunkCoord, TerrainView>();
        private readonly HashSet<long> seenUnits = new HashSet<long>();
        private readonly HashSet<ChunkCoord> seenTerrain = new HashSet<ChunkCoord>();
        private Guid runId;
        public void Initialize(GameCatalog catalog)
        {
            this.catalog = catalog; catalog.ValidatePresentation();
        }
        public void BeginFrame(Guid nextRun)
        {
            if (runId != nextRun)
            {
                foreach (var view in units.Values) if (view != null) Destroy(view.gameObject);
                foreach (var view in terrain.Values) if (view != null) Destroy(view.gameObject);
                units.Clear(); terrain.Clear(); runId = nextRun;
            }
            seenUnits.Clear(); seenTerrain.Clear();
        }
        public void ShowTerrain(ChunkData chunk, DVec2 relativeOrigin)
        {
            seenTerrain.Add(chunk.Coord);
            if (terrain.TryGetValue(chunk.Coord, out var existing) && existing != null && existing.Fingerprint != chunk.Fingerprint)
            { Destroy(existing.gameObject); terrain.Remove(chunk.Coord); }
            if (!terrain.TryGetValue(chunk.Coord, out var view))
            {
                var go = new GameObject("Chunk " + chunk.Coord.X + "," + chunk.Coord.Y); go.transform.SetParent(transform, false);
                view = go.AddComponent<TerrainView>(); view.Initialize(chunk, catalog.WorldMaterial); terrain.Add(chunk.Coord, view);
            }
            view.transform.localPosition = new Vector3((float)relativeOrigin.X, (float)relativeOrigin.Y, 0);
        }
        public void ShowUnit(UnitModel unit, DVec2 relativePosition)
        {
            long id = unit.Id; UnitKind kind = unit.Kind;
            seenUnits.Add(id);
            if (!units.TryGetValue(id, out var view))
            {
                var go = Instantiate(catalog.UnitViewPrefab, transform); go.name = kind + " #" + id;
                view = go.GetComponent<UnitView>(); units.Add(id, view);
            }
            view.Show(unit, relativePosition, catalog);
        }
        public void EndFrame(double elapsedSeconds, int enemyCount)
        {
            foreach (long id in new List<long>(units.Keys)) if (!seenUnits.Contains(id)) { if (units[id] != null) Destroy(units[id].gameObject); units.Remove(id); }
            foreach (var coord in new List<ChunkCoord>(terrain.Keys)) if (!seenTerrain.Contains(coord)) { if (terrain[coord] != null) Destroy(terrain[coord].gameObject); terrain.Remove(coord); }
        }
    }
}
