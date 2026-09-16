using System;

namespace SsalMuk.Core
{
    public interface IRunBuilder : IDisposable
    {
        RunModel Run { get; }
        void BuildWorld();
        void CreatePlayer();
        void CreateInitialEnemies();
    }
}
