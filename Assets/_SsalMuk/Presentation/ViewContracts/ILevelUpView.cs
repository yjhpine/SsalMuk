using System;
using System.Collections.Generic;
namespace SsalMuk.Presentation
{
    public interface IRewardHeadingView { void SetChestHeading(bool chest); }
    public interface ILevelUpView
    {
        event Action<int> ChoiceRequested;
        void Show(bool visible, bool canSubmit, string remaining, IReadOnlyList<string> titles, IReadOnlyList<string> descriptions);
    }
}
