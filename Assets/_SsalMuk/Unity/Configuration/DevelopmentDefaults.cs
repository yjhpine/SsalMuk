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

        public DefinitionCatalog CreateCatalog()
        {
            if (units == null) throw new ArgumentException("Unit definitions are missing.");
            return new DefinitionCatalog(units.Select(entry => entry == null
                ? throw new ArgumentException("A unit definition entry is missing.") : entry.ToDefinition()));
        }

        public string ValidationError
        {
            get
            {
                try { CreateCatalog(); return ""; }
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
