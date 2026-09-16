using System;

namespace SsalMuk.Presentation
{
    public interface IMainMenuView
    {
        event Action StartRequested;
        void Show(bool visible, bool canStart, string message);
    }
}
