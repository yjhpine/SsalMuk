using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using SsalMuk.Core;
using UnityEngine;

namespace SsalMuk.Unity
{
    [CreateAssetMenu(menuName = "SsalMuk/Development Defaults")]
    public sealed class DevelopmentDefaults : ScriptableObject
    {
        [SerializeField] private UnitEntry[] units = CreatePreset();
        [SerializeField] private WeaponEntry[] weapons = WeaponDefinition.DevelopmentPresets().Select(definition => new WeaponEntry(definition)).ToArray();
        [SerializeField, Range(0, 1)] private float obstacleChance = 0.2f;
        [SerializeField, Min(0)] private int initialEnemyCount = 12;
        [SerializeField, Min(1)] private int crowdIterations = 6;
        [SerializeField, Min(1)] private int navigationNodeBudget = 512;
        [SerializeField] private string firstLevelCost = "5";
        [SerializeField] private string levelCostStep = "3";
        [SerializeField] private double damageGrowth = 0.1;
        [SerializeField] private double speedGrowth = 0.1;
        [SerializeField] private double rangeGrowth = 0.08;
        [SerializeField] private double attractionRadius = 1.5;
        [SerializeField] private double experienceFlightSpeed = 6;
        [SerializeField] private double experienceOrbRadius = 0.08;
        [SerializeField] private string blueExperienceThreshold = "5";
        [SerializeField] private string redExperienceThreshold = "25";
        [SerializeField] private double acquisitionWeight = 1;
        [SerializeField] private double upgradeWeight = 3;
        public int InitialEnemyCount => initialEnemyCount >= 0 ? initialEnemyCount : throw new ArgumentOutOfRangeException(nameof(initialEnemyCount));
        public MapSettings CreateMapSettings() => new MapSettings(obstacleChance, maximumBodyRadius: CreateCatalog().Units.Max(unit => unit.BodyRadius));
        public MovementSettings CreateMovementSettings() => new MovementSettings(crowdIterations, navigationNodeBudget);
        public RewardWeights CreateRewardWeights() => new RewardWeights(acquisitionWeight, upgradeWeight);
        public GrowthSettings CreateGrowthSettings()
        {
            if (!BigInteger.TryParse(firstLevelCost, NumberStyles.Integer, CultureInfo.InvariantCulture, out var first) ||
                !BigInteger.TryParse(levelCostStep, NumberStyles.Integer, CultureInfo.InvariantCulture, out var step))
                throw new ArgumentException("Level costs must be integers.");
            return new GrowthSettings(first, step, damageGrowth, speedGrowth, rangeGrowth);
        }
        public PickupSettings CreatePickupSettings()
        {
            if (!BigInteger.TryParse(blueExperienceThreshold, NumberStyles.Integer, CultureInfo.InvariantCulture, out var blue) ||
                !BigInteger.TryParse(redExperienceThreshold, NumberStyles.Integer, CultureInfo.InvariantCulture, out var red))
                throw new ArgumentException("Experience thresholds must be integers.");
            var settings = new PickupSettings(attractionRadius, experienceFlightSpeed, experienceOrbRadius, blue, red);
            settings.ValidateForPlayer(CreateCatalog().GetUnit(UnitKind.Player).BodyRadius); return settings;
        }

        public DefinitionCatalog CreateCatalog()
        {
            if (units == null) throw new ArgumentException("Unit definitions are missing.");
            if (weapons == null) throw new ArgumentException("Weapon definitions are missing.");
            return new DefinitionCatalog(units.Select(entry => entry == null
                ? throw new ArgumentException("A unit definition entry is missing.") : entry.ToDefinition()),
                weapons.Select(entry => entry == null ? throw new ArgumentException("A weapon definition entry is missing.") : entry.ToDefinition()));
        }

        public string ValidationError
        {
            get
            {
                try { CreateCatalog(); CreateGrowthSettings(); CreatePickupSettings(); CreateRewardWeights(); return ""; }
                catch (ArgumentException error) { return error.Message; }
            }
        }

        // Approved development starting values; these are not final balance settings.
        private static UnitEntry[] CreatePreset() => new[] {
            new UnitEntry("player", UnitKind.Player, 100, 3, 0.28, 0, "0"),
            new UnitEntry("normal", UnitKind.Normal, 10, 1.5, 0.26, 5, "1"),
            new UnitEntry("air", UnitKind.Air, 6, 6, 0.22, 5, "1"),
            new UnitEntry("boss", UnitKind.Boss, 600, 1.1, 0.75, 15, "30")
        };

        [Serializable]
        public sealed class WeaponEntry
        {
            [SerializeField] private WeaponKind kind;
            [SerializeField] private double damage;
            [SerializeField] private double range;
            [SerializeField] private double periodSeconds;
            [SerializeField] private double activeSeconds;
            [SerializeField] private double width;
            [SerializeField] private double copySpacing;
            public WeaponEntry(WeaponDefinition definition)
            { kind = definition.Kind; damage = definition.Damage; range = definition.Range; periodSeconds = definition.PeriodSeconds;
                activeSeconds = definition.ActiveSeconds; width = definition.Width; copySpacing = definition.CopySpacing; }
            internal WeaponDefinition ToDefinition() => new WeaponDefinition(kind, damage, range, periodSeconds, activeSeconds, width, copySpacing);
        }

        [Serializable]
        public sealed class UnitEntry
        {
            [SerializeField] private string id;
            [SerializeField] private UnitKind kind;
            [SerializeField] private double maxHealth;
            [SerializeField] private double moveSpeed;
            [SerializeField] private double bodyRadius;
            [SerializeField] private double contactDamage;
            [SerializeField] private string experienceReward;

            public UnitEntry(string id, UnitKind kind, double maxHealth, double moveSpeed,
                double bodyRadius, double contactDamage, string experienceReward)
            {
                this.id = id; this.kind = kind; this.maxHealth = maxHealth; this.moveSpeed = moveSpeed;
                this.bodyRadius = bodyRadius; this.contactDamage = contactDamage; this.experienceReward = experienceReward;
            }

            internal UnitDefinition ToDefinition()
            {
                if (!BigInteger.TryParse(experienceReward, NumberStyles.Integer, CultureInfo.InvariantCulture, out var reward))
                    throw new ArgumentException("Experience reward must be an integer: " + id);
                return new UnitDefinition(id, kind, maxHealth, moveSpeed, bodyRadius, contactDamage, reward);
            }
        }
    }
}
