using System;
namespace SsalMuk.Presentation
{
    public interface IResultsView
    {
        event Action RestartRequested;
        event Action MenuRequested;
        void Show(bool visible, bool canSubmit, string survival, string kills, string level);
    }
}
