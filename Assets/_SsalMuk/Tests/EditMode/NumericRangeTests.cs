using System;
using System.Numerics;
using NUnit.Framework;
using SsalMuk.Core;
using SsalMuk.Unity;
using UnityEditor;
using UnityEngine;

namespace SsalMuk.Tests
{
    public sealed class NumericRangeTests
    {
        [Test]
        public void GrowthUsesSerializedCoefficientsAndRejectsInvalidConfiguration()
        {
            var defaults = ScriptableObject.CreateInstance<DevelopmentDefaults>();
            try
            {
                var serialized = new SerializedObject(defaults);
                serialized.FindProperty("firstLevelCost").stringValue = "7"; serialized.FindProperty("levelCostStep").stringValue = "2";
                serialized.FindProperty("damageGrowth").doubleValue = 0.5; serialized.ApplyModifiedPropertiesWithoutUndo();
                var settings = defaults.CreateGrowthSettings(); using var rig = RunTestRig.Create(growthSettings: settings);
                rig.GrantExperience(27); rig.Upgrade(WeaponKind.Sword, UpgradeKind.Damage);
                Assert.That(rig.Player.Level, Is.EqualTo(new BigInteger(4)));
                Assert.That(StatCalculator.Calculate(rig.Run.Definitions.GetWeapon(WeaponKind.Sword), rig.Player.Weapons.Get(WeaponKind.Sword), settings).Damage, Is.EqualTo(12));
                serialized.FindProperty("damageGrowth").doubleValue = double.NaN; serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(defaults.ValidationError, Is.Not.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(defaults); }
        }
        [Test]
        public void InvalidInputDoesNotMutateGrowthAndHugeDisplayRatiosRemainFinite()
        {
            using var rig = RunTestRig.Create();
            Assert.Throws<ArgumentOutOfRangeException>(() => rig.GrantExperience(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => rig.Upgrade(WeaponKind.Sword, UpgradeKind.Damage, -1));
            Assert.That(rig.Player.Growth.PendingChoices, Is.EqualTo(BigInteger.Zero));
            var huge = BigInteger.Pow(10, 400);
            Assert.That(NumberFormatter.Fraction(huge, huge * 2), Is.EqualTo(0.5));
            Assert.That(NumberFormatter.Format(huge), Is.EqualTo("1.00e400"));
        }
        [Test]
        public void PickupSettingsReadTheAssetValuesAndValidateThePlayerContactRadius()
        {
            var defaults = ScriptableObject.CreateInstance<DevelopmentDefaults>();
            try
            {
                var serialized = new SerializedObject(defaults);
                serialized.FindProperty("attractionRadius").doubleValue = 0.6;
                serialized.FindProperty("experienceFlightSpeed").doubleValue = 2;
                serialized.FindProperty("blueExperienceThreshold").stringValue = "12";
                serialized.FindProperty("redExperienceThreshold").stringValue = "200";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var settings = defaults.CreatePickupSettings();
                Assert.That(settings.AttractionRadius, Is.EqualTo(0.6)); Assert.That(settings.FlightSpeed, Is.EqualTo(2));
                Assert.That(settings.TierFor(12), Is.EqualTo(ExperienceTier.Blue)); Assert.That(settings.TierFor(199), Is.EqualTo(ExperienceTier.Blue));
                Assert.That(settings.TierFor(200), Is.EqualTo(ExperienceTier.Red));
                serialized.FindProperty("attractionRadius").doubleValue = 0.3; serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(defaults.ValidationError, Does.Contain("contact"));
            }
            finally { UnityEngine.Object.DestroyImmediate(defaults); }
        }
    }
}
