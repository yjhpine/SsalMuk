using System;
namespace SsalMuk.Core
{
    public sealed class SpawnTicket
    {
        public Guid RunId { get; }
        public long ScheduleId { get; }
        public UnitKind Kind { get; }
        public double ScheduledAt { get; }
        public long Count { get; }
        public long Remaining { get; internal set; }
        public bool IsSurroundWave => Wave != null;
        internal SurroundWave Wave { get; set; }
        internal AirGroupSpawner AirLayout { get; set; }
        internal UnitDefinition AirDefinition { get; set; }
        internal SpawnTicket(Guid runId, long scheduleId, UnitKind kind, double scheduledAt, long count)
        { RunId = runId; ScheduleId = scheduleId; Kind = kind; ScheduledAt = scheduledAt; Count = Remaining = count; }
    }
}
