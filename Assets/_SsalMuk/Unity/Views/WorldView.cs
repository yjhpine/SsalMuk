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
        private Text timer, count;
        public void Initialize(GameCatalog catalog)
        {
            this.catalog = catalog; catalog.ValidatePresentation();
            var hud = UiFactory.Canvas(transform, "BattleHud", 10);
            var top = UiFactory.Rect(hud.transform, "TopBar", new Vector2(0, 74), new Vector2(0, 1), new Vector2(0, -37));
            top.anchorMax = new Vector2(1, 1); top.gameObject.AddComponent<Image>().color = new Color(0.035f, 0.07f, 0.07f, 0.9f);
            UiFactory.Label(hud.transform, "RunLabel", catalog.UiFont, "생존 중", 19, UiFactory.Mint, new Vector2(150, 60), new Vector2(0, 1), new Vector2(93, -36), TextAnchor.MiddleLeft);
            timer = UiFactory.Label(hud.transform, "SurvivalTime", catalog.UiFont, "00:00", 28, Color.white, new Vector2(190, 60), new Vector2(0.5f, 1), new Vector2(0, -36));
            count = UiFactory.Label(hud.transform, "EnemyCount", catalog.UiFont, "", 19, UiFactory.Muted, new Vector2(230, 60), new Vector2(1, 1), new Vector2(-138, -36), TextAnchor.MiddleRight);
            UiFactory.Label(hud.transform, "PreviewNotice", catalog.UiFont, "이동 미리보기 · 전투 준비 중", 16, UiFactory.Muted, new Vector2(650, 40), new Vector2(0.5f, 0), new Vector2(0, 24));
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
        public void ShowUnit(long id, UnitKind kind, DVec2 relativePosition, double bodyRadius)
        {
            seenUnits.Add(id);
            if (!units.TryGetValue(id, out var view))
            {
                var go = Instantiate(catalog.UnitViewPrefab, transform); go.name = kind + " #" + id;
                view = go.GetComponent<UnitView>(); units.Add(id, view);
            }
            view.Show(id, kind, relativePosition, bodyRadius, catalog);
        }
        public void EndFrame(double elapsedSeconds, int enemyCount)
        {
            foreach (long id in new List<long>(units.Keys)) if (!seenUnits.Contains(id)) { if (units[id] != null) Destroy(units[id].gameObject); units.Remove(id); }
            foreach (var coord in new List<ChunkCoord>(terrain.Keys)) if (!seenTerrain.Contains(coord)) { if (terrain[coord] != null) Destroy(terrain[coord].gameObject); terrain.Remove(coord); }
            long seconds = (long)elapsedSeconds; timer.text = (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00"); count.text = "몬스터  " + enemyCount;
        }
    }
}
