using System;

namespace SsalMuk.Core
{
    public sealed class WeaponDefinition
    {
        public WeaponKind Kind { get; }
        public double Damage { get; }
        public double Range { get; }
        public double PeriodSeconds { get; }
        public double ActiveSeconds { get; }
        public double Width { get; }
        public double CopySpacing { get; }
        public string VisualKey => Kind.ToString();
        public WeaponDefinition(WeaponKind kind, double damage, double range, double periodSeconds, double activeSeconds,
            double width = 0.35, double copySpacing = 0.25)
        {
            if (!Enum.IsDefined(typeof(WeaponKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            RequirePositive(damage, nameof(damage)); RequirePositive(range, nameof(range));
            RequirePositive(periodSeconds, nameof(periodSeconds)); RequirePositive(activeSeconds, nameof(activeSeconds));
            RequirePositive(width, nameof(width)); RequirePositive(copySpacing, nameof(copySpacing));
            Kind = kind; Damage = damage; Range = range; PeriodSeconds = periodSeconds; ActiveSeconds = activeSeconds; Width = width; CopySpacing = copySpacing;
        }
        internal static void RequirePositive(double value, string name)
        {
            if (!(value > 0) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(name, "A finite positive value is required.");
        }
        public WeaponStats BaseStats => new WeaponStats(Damage, Range, PeriodSeconds);
        public static WeaponDefinition[] DevelopmentPresets() => new[] {
            new WeaponDefinition(WeaponKind.Sword, 8, 1.6, 1, 0.25), new WeaponDefinition(WeaponKind.Spear, 10, 2.2, 1.2, 0.18),
            new WeaponDefinition(WeaponKind.Axe, 6, 1.8, 1.6, 0.6, 0.25), new WeaponDefinition(WeaponKind.Fireball, 6, 0.8, 1.4, 0.2, 0.1)
        };
    }
}
