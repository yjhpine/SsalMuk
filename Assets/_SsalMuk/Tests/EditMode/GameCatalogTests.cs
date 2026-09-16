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
            Assert.That(core.GetUnit(UnitKind.Normal).MoveSpeed, Is.EqualTo(1.5));
            Assert.That(core.GetUnit(UnitKind.Air).MoveSpeed, Is.EqualTo(6));
            Assert.That(core.GetUnit(UnitKind.Boss).MaxHealth, Is.EqualTo(600));
            Assert.That(catalog.UnitViewPrefab, Is.SameAs(prefab));
            Assert.That(catalog.GetSprite(UnitKind.Air), Is.SameAs(sprite));
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
