using System;
using System.Linq;
using NUnit.Framework;
using SsalMuk.Core;
using SsalMuk.Unity;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SsalMuk.Tests
{
    public sealed class GameCatalogTests
    {
        private DevelopmentDefaults definitions;
        private GameCatalog catalog;
        private GameObject prefab;
        private Sprite sprite;

        [SetUp]
        public void CreateConfiguration()
        {
            definitions = ScriptableObject.CreateInstance<DevelopmentDefaults>();
            catalog = ScriptableObject.CreateInstance<GameCatalog>();
            prefab = new GameObject("CatalogTestPrefab");
            sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0));
        }

        [TearDown]
        public void DisposeConfiguration()
        {
            Object.DestroyImmediate(catalog); Object.DestroyImmediate(definitions);
            Object.DestroyImmediate(prefab); Object.DestroyImmediate(sprite);
        }

        private GameCatalog.UnitVisual[] Visuals() => Enum.GetValues(typeof(UnitKind)).Cast<UnitKind>()
            .Select(kind => new GameCatalog.UnitVisual(kind, sprite)).ToArray();

        [Test]
        public void DevelopmentPresetAndDisplayReferencesCreateAValidatedCatalog()
        {
            catalog.Configure(definitions, prefab, Visuals());
            var core = catalog.CreateDefinitions();
            Assert.That(core.GetUnit(UnitKind.Player).MaxHealth, Is.EqualTo(100));
            Assert.That(core.GetUnit(UnitKind.Normal).MoveSpeed, Is.EqualTo(2.85).Within(1e-9));
            Assert.That(core.GetUnit(UnitKind.Air).MoveSpeed, Is.EqualTo(3.3).Within(1e-9));
            Assert.That(core.GetUnit(UnitKind.Boss).MaxHealth, Is.EqualTo(600));
            Assert.That(core.GetUnit(UnitKind.Boss).ContactDamage, Is.EqualTo(60));
            Assert.That(catalog.UnitViewPrefab, Is.SameAs(prefab));
            Assert.That(catalog.GetSprite(UnitKind.Air), Is.SameAs(sprite));
        }

        [Test]
        public void ActiveCatalogUsesSixtyBossDamageAndChaseSpeedRoundsToOneDecimal()
        {
            var active = Resources.Load<GameCatalog>("Bootstrap/GameCatalog").CreateDefinitions();
            Assert.That(active.GetUnit(UnitKind.Boss).ContactDamage, Is.EqualTo(60));
            var serialized = new SerializedObject(definitions);
            serialized.FindProperty("units").GetArrayElementAtIndex(0).FindPropertyRelative("moveSpeed").doubleValue = 3.146;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var configured = definitions.CreateCatalog();
            Assert.That(configured.GetUnit(UnitKind.Boss).MoveSpeed, Is.EqualTo(3.8));
            Assert.That(configured.GetUnit(UnitKind.Normal).MoveSpeed, Is.EqualTo(3.146 * .95).Within(1e-9));
        }

        [Test]
        public void PresetAndActiveCatalogIncreaseOrdinarySpawnRateByHalf()
        {
            foreach (var defaults in new[] { definitions, Resources.Load<GameCatalog>("Bootstrap/GameCatalog").Defaults })
            {
                var spawns = defaults.CreateSpawnSettings();
                Assert.That(spawns.NormalBaseRate, Is.EqualTo(1.5));
                Assert.That(spawns.NormalRateGrowth, Is.EqualTo(.0125).Within(1e-12));
                Assert.That(spawns.NormalPopulationCap, Is.EqualTo(500));
                Assert.That(spawns.AirBaseCount, Is.EqualTo(12));
                Assert.That(spawns.AirInterval, Is.EqualTo(20));
                var difficulty = new DifficultyCurve(spawns);
                Assert.That(difficulty.AirCount(120), Is.EqualTo(14));
                Assert.That(difficulty.AirCount(600), Is.EqualTo(24));
                Assert.That(difficulty.AirCount(3600), Is.EqualTo(24));
                Assert.That(defaults.InitialEnemyCount, Is.EqualTo(12));
            }
        }

        [Test]
        public void MissingOrDuplicatedDisplayReferencesAreRejected()
        {
            Assert.Throws<ArgumentException>(() => catalog.Configure(null, prefab, Visuals()));
            Assert.Throws<ArgumentException>(() => catalog.Configure(definitions, null, Visuals()));
            Assert.Throws<ArgumentException>(() => catalog.Configure(definitions, prefab, Visuals().Take(3)));
            Assert.Throws<ArgumentException>(() => catalog.Configure(definitions, prefab, Visuals().Concat(new[] { Visuals()[0] })));
        }

        [Test]
        public void InvalidSerializedRadiusIsCaughtBeforeRuntimeUse()
        {
            catalog.Configure(definitions, prefab, Visuals());
            var serialized = new SerializedObject(definitions);
            serialized.FindProperty("units").GetArrayElementAtIndex(0).FindPropertyRelative("bodyRadius").doubleValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.Throws<ArgumentOutOfRangeException>(() => catalog.CreateDefinitions());
            Assert.That(definitions.ValidationError, Is.Not.Empty);
        }

        [Test]
        public void RuntimeDefinitionsDoNotChangeWhenTheEditorAssetChanges()
        {
            var original = definitions.CreateCatalog();
            var serialized = new SerializedObject(definitions);
            serialized.FindProperty("units").GetArrayElementAtIndex(0).FindPropertyRelative("maxHealth").doubleValue = 150;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(original.GetUnit(UnitKind.Player).MaxHealth, Is.EqualTo(100));
            Assert.That(definitions.CreateCatalog().GetUnit(UnitKind.Player).MaxHealth, Is.EqualTo(150));
        }
    }
}
