using System;
using SsalMuk.Core;

namespace SsalMuk.Presentation
{
    public interface IWorldView
    {
        void BeginFrame(Guid runId);
        void ShowTerrain(ChunkData chunk, DVec2 relativeOrigin);
        void ShowUnit(long id, UnitKind kind, DVec2 relativePosition, double bodyRadius);
        void EndFrame(double elapsedSeconds, int enemyCount);
    }
}
