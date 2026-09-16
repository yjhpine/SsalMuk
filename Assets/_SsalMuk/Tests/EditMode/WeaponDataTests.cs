using System;
using System.Linq;
using NUnit.Framework;
using SsalMuk.Core;
using SsalMuk.Unity;
using UnityEditor;
using UnityEngine;

namespace SsalMuk.Tests
{
    public sealed class WeaponDataTests
    {
        [Test]
        public void InvalidDefinitionsAndDerivedStatsAreRejectedBeforeStarting()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponDefinition(WeaponKind.Sword, 8, 1.6, 0, 0.25));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponDefinition(WeaponKind.Sword, 8, double.NaN, 1, 0.25));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponStats(8, 1.6, 1, copies: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponStats(double.PositiveInfinity, 1.6, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponDefinition(WeaponKind.Fireball, 6, 0.8, 1.4, 0.2, projectileSpeed: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponDefinition(WeaponKind.Fireball, 6, 0.8, 1.4, 0.2, projectileLifetime: double.PositiveInfinity));
        }

        [Test]
        public void WeaponCatalogRejectsIncompleteOrDuplicateKinds()
        {
            using var rig = RunTestRig.Create(); var weapons = WeaponDefinition.DevelopmentPresets();
            Assert.Throws<ArgumentException>(() => new DefinitionCatalog(rig.Run.Definitions.Units, weapons.Take(3)));
            Assert.Throws<ArgumentException>(() => new DefinitionCatalog(rig.Run.Definitions.Units, weapons.Concat(new[] { weapons[0] })));
        }

        [Test]
        public void SerializedWeaponBalanceReachesTheRuntimeDefinition()
        {
            var defaults = ScriptableObject.CreateInstance<DevelopmentDefaults>();
            try
            {
                var serialized = new SerializedObject(defaults);
                serialized.FindProperty("weapons").GetArrayElementAtIndex(0).FindPropertyRelative("damage").doubleValue = 14;
                serialized.FindProperty("weapons").GetArrayElementAtIndex(3).FindPropertyRelative("projectileSpeed").doubleValue = 2;
                serialized.FindProperty("weapons").GetArrayElementAtIndex(3).FindPropertyRelative("projectileLifetime").doubleValue = 0.5;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(defaults.CreateCatalog().GetWeapon(WeaponKind.Sword).BaseStats.Damage, Is.EqualTo(14));
                Assert.That(defaults.CreateCatalog().GetWeapon(WeaponKind.Fireball).ProjectileSpeed, Is.EqualTo(2));
                Assert.That(defaults.CreateCatalog().GetWeapon(WeaponKind.Fireball).ProjectileLifetime, Is.EqualTo(0.5));
                serialized.FindProperty("weapons").GetArrayElementAtIndex(0).FindPropertyRelative("periodSeconds").doubleValue = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(defaults.ValidationError, Is.Not.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(defaults); }
        }
    }
}
