using System;
using System.Collections.Generic;
namespace SsalMuk.Core
{
    // Only the latest wave is a pressure sample. Removing a living unit is never a kill.
    internal sealed class SurroundWaveSchedule
    {
        private readonly SurroundWaveSettings settings;
        private readonly HashSet<long> tracked = new HashSet<long>();
        private long nextOrdinal = 1;
        private int sampleCount, fastKills;
        private double sampleStart;
        public long Number { get; private set; }
        public int Stage { get; private set; } = 1;
        public SurroundWaveSchedule(SurroundWaveSettings settings) => this.settings = settings;
        public bool IsDue(double now) => settings.BaseCount > 0 && nextOrdinal * SurroundWaveSettings.Interval <= now;
        public bool TryTake(double now, int normalPopulation, UnitDefinition baseline, out SurroundWave wave)
        {
            wave = null;
            double due = nextOrdinal * SurroundWaveSettings.Interval;
            if (settings.BaseCount == 0 || due > now) return false;
            long following = checked(nextOrdinal + 1);
            if (!(following * SurroundWaveSettings.Interval > due)) throw new NumericRangeException("Surrounding wave deadline lost precision.");
            if (sampleCount > 0 && fastKills >= sampleCount * settings.FastKillFraction &&
                normalPopulation <= sampleCount * settings.RemainingFraction) Stage = checked(Stage + 1);
            Number = nextOrdinal; nextOrdinal = following;
            tracked.Clear(); sampleCount = fastKills = 0; sampleStart = now;
            double health = baseline.MaxHealth * (1 + (Stage - 1.0) * settings.HealthStep);
            double damage = baseline.ContactDamage * (1 + (Stage - 1.0) * settings.DamageStep);
            if (double.IsInfinity(health) || double.IsInfinity(damage)) throw new NumericRangeException("Wave difficulty exceeds numeric range.");
            var definition = new UnitDefinition(baseline.Id, baseline.Kind, health, baseline.MoveSpeed, baseline.BodyRadius,
                damage, baseline.ExperienceReward, baseline.HurtRadius, baseline.VisualFootOffset);
            wave = new SurroundWave(Number, due, now + settings.ReservationSeconds,
                checked(settings.BaseCount + (long)(Stage - 1) * settings.CountStep), definition);
            return true;
        }
        public void Spawned(SurroundWave wave, UnitModel unit)
        {
            if (wave.Number != Number) return;
            tracked.Add(unit.Id); sampleCount++;
        }
        public void Removed(UnitModel unit, double now)
        {
            if (tracked.Remove(unit.Id) && !unit.IsAlive && now <= sampleStart + settings.FastClearSeconds) fastKills++;
        }
        public void Clear() => tracked.Clear();
    }

    internal sealed class SurroundWave
    {
        public long Number { get; }
        public double ScheduledAt { get; }
        public double ExpiresAt { get; }
        public long RequestedCount { get; }
        public UnitDefinition Definition { get; }
        public SurroundWave(long number, double scheduledAt, double expiresAt, long requestedCount, UnitDefinition definition)
        { Number = number; ScheduledAt = scheduledAt; ExpiresAt = expiresAt; RequestedCount = requestedCount; Definition = definition; }
    }
}
