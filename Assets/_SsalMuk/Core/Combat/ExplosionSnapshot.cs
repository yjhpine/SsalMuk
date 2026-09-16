namespace SsalMuk.Core
{
    public sealed class ExplosionSnapshot
    {
        public HitKey Key { get; }
        public WorldPosition Position { get; }
        public double Radius { get; }
        public double Time { get; }
        public long FirstHitTargetId { get; }
        public double Duration { get; }
        public ExplosionSnapshot(HitKey key, WorldPosition position, double radius, double time, long firstHitTargetId, double duration = 0.2)
        { Key = key; Position = position; Radius = radius; Time = time; FirstHitTargetId = firstHitTargetId; Duration = duration; }
    }
}
