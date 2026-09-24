using System.Collections.Generic;
using SsalMuk.Core;

namespace SsalMuk.Presentation
{
    public interface IItemWorldView { void ShowItem(PowerupRecord item, DVec2 relative); }
    public interface IStatusHudView { void ShowStatus(string status); }
}
