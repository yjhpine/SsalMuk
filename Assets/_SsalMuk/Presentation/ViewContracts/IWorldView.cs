using System;
using SsalMuk.Core;

namespace SsalMuk.Presentation
{
    public interface IWorldView
    {
        void BeginFrame(Guid runId);
        void ShowTerrain(ChunkData chunk, DVec2 relativeOrigin);
        void ShowUnit(UnitModel unit, DVec2 relativePosition);
        void EndFrame(double elapsedSeconds, int enemyCount);
    }
}
