using System;
using System.Collections.Generic;

namespace SsalMuk.Core
{
    public interface IRunReadModel
    {
        Guid RunId { get; }
        RunPhase Phase { get; }
        RunClock Clock { get; }
        WorldPosition ViewOrigin { get; }
        IReadOnlyCollection<UnitModel> Units { get; }
        IReadOnlyCollection<ExperienceRecord> Experience { get; }
        IReadOnlyCollection<ChunkData> Terrain { get; }
        PickupSettings PickupSettings { get; }
        ICombatReadModel Combat { get; }
    }
}
