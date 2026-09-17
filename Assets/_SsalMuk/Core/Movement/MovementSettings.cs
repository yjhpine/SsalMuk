using System;

namespace SsalMuk.Core
{
    public sealed class MovementSettings
    {
        public double CrowdAvoidanceStrength { get; }
        public int NavigationNodeBudget { get; }
        public int SlideContacts { get; }
        public MovementSettings(int navigationNodeBudget = 512, int slideContacts = 4, double crowdAvoidanceStrength = 0.65)
        {
            if (navigationNodeBudget <= 0 || slideContacts <= 0) throw new ArgumentOutOfRangeException(nameof(navigationNodeBudget));
            if (double.IsNaN(crowdAvoidanceStrength) || crowdAvoidanceStrength < 0 || crowdAvoidanceStrength > 1) throw new ArgumentOutOfRangeException(nameof(crowdAvoidanceStrength));
            CrowdAvoidanceStrength = crowdAvoidanceStrength; NavigationNodeBudget = navigationNodeBudget; SlideContacts = slideContacts;
        }
    }
}
