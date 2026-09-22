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
        [SerializeField, Range(0, 1)] private float crowdAvoidanceStrength = 0.65f;
        [SerializeField, Min(1)] private int navigationNodeBudget = 512;
        [SerializeField] private string firstLevelCost = "5";
        [SerializeField] private string levelCostStep = "3";
        [SerializeField] private double damageGrowth = 0.1;
        [SerializeField] private double speedGrowth = 0.1;
        [SerializeField] private double rangeGrowth = 0.08;
        [SerializeField] private double attractionRadius = 1.95;
        [SerializeField] private double experienceFlightSpeed = 6;
        [SerializeField] private double experienceOrbRadius = 0.08;
        [SerializeField] private string blueExperienceThreshold = "5";
        [SerializeField] private string redExperienceThreshold = "25";
        [SerializeField] private double acquisitionWeight = 1;
        [SerializeField] private double upgradeWeight = 3;
        [SerializeField] private double normalBaseRate = 1;
        [SerializeField] private double normalRateGrowth = 1.0 / 120;
        [SerializeField, Min(1)] private int normalPopulationCap = 500;
        [SerializeField] private double airInterval = 20;
        [SerializeField] private int airBaseCount = 8;
        [SerializeField] private double airCountGrowthSeconds = 120;
        [SerializeField] private double airSpacing = 0.65;
        [SerializeField] private double groundSpawnMargin = 1.2;
        [SerializeField] private double groundSpawnBand = 5;
        [SerializeField] private int spawnPositionAttempts = 32;
        [SerializeField] private int spawnCreationBudget = 64;
        [SerializeField] private double airDepartureMargin = 4;
        [SerializeField, Min(0)] private int surroundWaveBaseCount = 80;
        [SerializeField, Min(1)] private int surroundWaveCountStep = 20;
        [SerializeField] private double surroundWaveHealthStep = .2;
        [SerializeField] private double surroundWaveDamageStep = .1;
        [SerializeField] private double surroundWaveFastClearSeconds = 45;
        [SerializeField] private double surroundWaveFastKillFraction = .8;
        [SerializeField] private double surroundWaveRemainingFraction = .25;
        [SerializeField] private double surroundWaveReservationSeconds = 5;
        [SerializeField, Min(.1f)] private float cameraOrthographicSize = 8;
        public float CameraOrthographicSize => cameraOrthographicSize > 0 && !float.IsInfinity(cameraOrthographicSize)
            ? cameraOrthographicSize : throw new ArgumentOutOfRangeException(nameof(cameraOrthographicSize));
        public int InitialEnemyCount => initialEnemyCount >= 0 ? initialEnemyCount : throw new ArgumentOutOfRangeException(nameof(initialEnemyCount));
        public MapSettings CreateMapSettings() => new MapSettings(obstacleChance, maximumBodyRadius: CreateCatalog().Units.Max(unit => unit.BodyRadius));
        public MovementSettings CreateMovementSettings() => new MovementSettings(navigationNodeBudget: navigationNodeBudget, crowdAvoidanceStrength: crowdAvoidanceStrength);
        public RewardWeights CreateRewardWeights() => new RewardWeights(acquisitionWeight, upgradeWeight);
        public SpawnSettings CreateSpawnSettings() => new SpawnSettings(normalBaseRate, normalRateGrowth, airInterval, airBaseCount,
            airCountGrowthSeconds, airSpacing, groundSpawnMargin, groundSpawnBand, spawnPositionAttempts, spawnCreationBudget, airDepartureMargin, normalPopulationCap,
            new SurroundWaveSettings(surroundWaveBaseCount, surroundWaveCountStep, surroundWaveHealthStep, surroundWaveDamageStep,
                surroundWaveFastClearSeconds, surroundWaveFastKillFraction, surroundWaveRemainingFraction, surroundWaveReservationSeconds));
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
            var catalog = new DefinitionCatalog(units.Select(entry => entry == null
                ? throw new ArgumentException("A unit definition entry is missing.") : entry.ToDefinition()),
                weapons.Select(entry => entry == null ? throw new ArgumentException("A weapon definition entry is missing.") : entry.ToDefinition()));
            double playerSpeed = catalog.GetUnit(UnitKind.Player).MoveSpeed;
            return new DefinitionCatalog(catalog.Units.Select(unit => unit.Kind == UnitKind.Player ? unit :
                new UnitDefinition(unit.Id, unit.Kind, unit.MaxHealth, playerSpeed * (unit.Kind == UnitKind.Boss ? 1.2 : 1.1),
                    unit.BodyRadius, unit.ContactDamage, unit.ExperienceReward, unit.HurtRadius, unit.VisualFootOffset)),
                Enum.GetValues(typeof(WeaponKind)).Cast<WeaponKind>().Select(catalog.GetWeapon));
        }

        public string ValidationError
        {
            get
            {
                try { CreateCatalog(); CreateGrowthSettings(); CreatePickupSettings(); CreateRewardWeights(); CreateSpawnSettings(); _ = CameraOrthographicSize; return ""; }
                catch (ArgumentException error) { return error.Message; }
            }
        }

        // Approved development starting values; these are not final balance settings.
        private static UnitEntry[] CreatePreset() => new[] {
            new UnitEntry("player", UnitKind.Player, 100, 3, .24896384, 0, "0", .34232528, .28),
            new UnitEntry("normal", UnitKind.Normal, 10, 3.3, .325, 5, "1", .3575, .26),
            new UnitEntry("air", UnitKind.Air, 6, 3.3, .265, 5, "1", .2915, .22),
            new UnitEntry("boss", UnitKind.Boss, 600, 3.6, 1.125, 15, "30", 1.2375, .75)
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
            [SerializeField] private double projectileSpeed = 8;
            [SerializeField] private double projectileLifetime = 3;
            public WeaponEntry(WeaponDefinition definition)
            { kind = definition.Kind; damage = definition.Damage; range = definition.Range; periodSeconds = definition.PeriodSeconds;
                activeSeconds = definition.ActiveSeconds; width = definition.Width; copySpacing = definition.CopySpacing;
                projectileSpeed = definition.ProjectileSpeed; projectileLifetime = definition.ProjectileLifetime; }
            internal WeaponDefinition ToDefinition() => new WeaponDefinition(kind, damage, range, periodSeconds, activeSeconds, width, copySpacing, projectileSpeed, projectileLifetime);
        }

        [Serializable]
        public sealed class UnitEntry
        {
            [SerializeField] private string id;
            [SerializeField] private UnitKind kind;
            [SerializeField] private double maxHealth;
            [SerializeField] private double moveSpeed;
            [SerializeField] private double bodyRadius;
            [SerializeField] private double hurtRadius;
            [SerializeField] private double visualFootOffset;
            [SerializeField] private double contactDamage;
            [SerializeField] private string experienceReward;

            public UnitEntry(string id, UnitKind kind, double maxHealth, double moveSpeed,
                double bodyRadius, double contactDamage, string experienceReward, double hurtRadius = 0, double visualFootOffset = 0)
            {
                this.id = id; this.kind = kind; this.maxHealth = maxHealth; this.moveSpeed = moveSpeed;
                this.bodyRadius = bodyRadius; this.contactDamage = contactDamage; this.experienceReward = experienceReward;
                this.hurtRadius = hurtRadius; this.visualFootOffset = visualFootOffset;
            }

            internal UnitDefinition ToDefinition()
            {
                if (!BigInteger.TryParse(experienceReward, NumberStyles.Integer, CultureInfo.InvariantCulture, out var reward))
                    throw new ArgumentException("Experience reward must be an integer: " + id);
                return new UnitDefinition(id, kind, maxHealth, moveSpeed, bodyRadius, contactDamage, reward,
                    hurtRadius == 0 ? (double?)null : hurtRadius, visualFootOffset == 0 ? (double?)null : visualFootOffset);
            }
        }
    }
}
