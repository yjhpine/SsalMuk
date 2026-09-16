using System;

namespace SsalMuk.Core
{
    public sealed class MovementSettings
    {
        public int CrowdIterations { get; }
        public int NavigationNodeBudget { get; }
        public int SlideContacts { get; }
        public MovementSettings(int crowdIterations = 6, int navigationNodeBudget = 512, int slideContacts = 4)
        {
            if (crowdIterations <= 0 || navigationNodeBudget <= 0 || slideContacts <= 0) throw new ArgumentOutOfRangeException(nameof(crowdIterations));
            CrowdIterations = crowdIterations; NavigationNodeBudget = navigationNodeBudget; SlideContacts = slideContacts;
        }
    }
}
