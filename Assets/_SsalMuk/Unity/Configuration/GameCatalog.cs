using System;
using System.Collections.Generic;
using System.Linq;
using SsalMuk.Core;
using UnityEngine;

namespace SsalMuk.Unity
{
    [CreateAssetMenu(menuName = "SsalMuk/Game Catalog")]
    public sealed class GameCatalog : ScriptableObject
    {
        [SerializeField] private DevelopmentDefaults unitDefinitions;
        [SerializeField] private GameObject unitViewPrefab;
        [SerializeField] private UnitVisual[] unitVisuals = Array.Empty<UnitVisual>();
        [SerializeField] private Font uiFont;
        [SerializeField] private Sprite solidSprite;
        [SerializeField] private Material worldMaterial;
        [SerializeField] private VisualCatalog visualCatalog;
        public VisualCatalog Visuals => visualCatalog;
        public GameObject UnitViewPrefab => unitViewPrefab;
        public Font UiFont => uiFont;
        public Sprite SolidSprite => solidSprite;
        public Material WorldMaterial => worldMaterial;
        public DevelopmentDefaults Defaults => unitDefinitions;
        public void ConfigureVisualCatalog(VisualCatalog visuals)
        {
            visuals.Validate(); visualCatalog = visuals;
            unitVisuals = Enum.GetValues(typeof(UnitKind)).Cast<UnitKind>().Select(kind => new UnitVisual(kind, visuals.Unit(kind).Sprite)).ToArray();
        }

        public void ConfigurePresentation(Font font, Sprite solid, Material material)
        {
            if (font == null || solid == null || material == null) throw new ArgumentException("Font, solid sprite and world material are required.");
            uiFont = font; solidSprite = solid; worldMaterial = material;
        }

        public void ValidatePresentation()
        {
            CreateDefinitions();
            if (uiFont == null || solidSprite == null || worldMaterial == null || unitViewPrefab.GetComponent<UnitView>() == null)
                throw new ArgumentException("Runtime presentation content is incomplete.");
            if (visualCatalog == null) throw new ArgumentException("The final art catalog is missing.");
            visualCatalog.Validate();
        }

        public void Configure(DevelopmentDefaults definitions, GameObject prefab, IEnumerable<UnitVisual> visuals)
        {
            if (visuals == null) throw new ArgumentException("Display references are required.", nameof(visuals));
            UnitVisual[] copy = visuals.ToArray();
            ValidateBindings(definitions, prefab, copy);
            unitDefinitions = definitions; unitViewPrefab = prefab; unitVisuals = copy;
        }

        public DefinitionCatalog CreateDefinitions() => ValidateBindings(unitDefinitions, unitViewPrefab, unitVisuals);

        public Sprite GetSprite(UnitKind kind)
        {
            var visual = unitVisuals?.FirstOrDefault(value => value != null && value.Kind == kind);
            if (visual == null || visual.Sprite == null) throw new KeyNotFoundException("Missing sprite: " + kind);
            return visual.Sprite;
        }

        public string ValidationError
        {
            get
            {
                try { CreateDefinitions(); return ""; }
                catch (ArgumentException error) { return error.Message; }
            }
        }

        private static DefinitionCatalog ValidateBindings(DevelopmentDefaults definitions, GameObject prefab, UnitVisual[] visuals)
        {
            if (definitions == null) throw new ArgumentException("Unit definition asset is required.");
            DefinitionCatalog catalog = definitions.CreateCatalog();
            if (prefab == null) throw new ArgumentException("Unit view prefab is required.");
            if (visuals == null) throw new ArgumentException("Unit display references are required.");
            var kinds = new HashSet<UnitKind>();
            foreach (var visual in visuals)
            {
                if (visual == null || !Enum.IsDefined(typeof(UnitKind), visual.Kind) || visual.Sprite == null || !kinds.Add(visual.Kind))
                    throw new ArgumentException("Each unit kind requires one valid sprite reference.");
            }
            foreach (var definition in catalog.Units)
                if (!kinds.Contains(definition.Kind)) throw new ArgumentException("Missing unit display reference: " + definition.Kind);
            return catalog;
        }

        [Serializable]
        public sealed class UnitVisual
        {
            [SerializeField] private UnitKind kind;
            [SerializeField] private Sprite sprite;
            public UnitKind Kind => kind;
            public Sprite Sprite => sprite;
            public UnitVisual(UnitKind kind, Sprite sprite) { this.kind = kind; this.sprite = sprite; }
        }
    }
}
