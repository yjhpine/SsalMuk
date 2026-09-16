using System;
using System.Numerics;

namespace SsalMuk.Core
{
    public sealed class UnitDefinition
    {
        public string Id { get; }
        public UnitKind Kind { get; }
        public double MaxHealth { get; }
        public double MoveSpeed { get; }
        public double BodyRadius { get; }
        public double ContactDamage { get; }
        public BigInteger ExperienceReward { get; }

        public UnitDefinition(string id, UnitKind kind, double maxHealth, double moveSpeed,
            double bodyRadius, double contactDamage, BigInteger experienceReward)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Definition ID is required.", nameof(id));
            if (!Enum.IsDefined(typeof(UnitKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            RequirePositive(maxHealth, nameof(maxHealth));
            RequirePositive(moveSpeed, nameof(moveSpeed));
            RequirePositive(bodyRadius, nameof(bodyRadius));
            if (contactDamage < 0 || double.IsNaN(contactDamage) || double.IsInfinity(contactDamage))
                throw new ArgumentOutOfRangeException(nameof(contactDamage));
            if (experienceReward.Sign < 0) throw new ArgumentOutOfRangeException(nameof(experienceReward));
            Id = id; Kind = kind; MaxHealth = maxHealth; MoveSpeed = moveSpeed;
            BodyRadius = bodyRadius; ContactDamage = contactDamage; ExperienceReward = experienceReward;
        }

        private static void RequirePositive(double value, string parameter)
        {
            if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameter, "Value must be positive and finite.");
        }
    }
}
