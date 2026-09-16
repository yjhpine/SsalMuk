using System.Collections.Generic;

namespace SsalMuk.Core
{
    public interface IWorldQuery
    {
        long? FindNearestEnemy(WorldPosition position);
        IReadOnlyList<long> QueryCircle(WorldPosition position, double radius);
        IReadOnlyList<long> QueryExperienceCircle(WorldPosition position, double radius);
        SweepHit? SweepCircle(WorldPosition position, DVec2 displacement, double radius);
        bool IsCircleFree(WorldPosition position, double radius);
    }
}
