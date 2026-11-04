namespace SsalMuk.Presentation
{
    public interface IMenuNavigationView
    {
        bool IsSettingsVisible { get; }
        void SetMenuBlocked(bool blocked);
        void ShowPause(bool visible);
        void ShowSettings(bool visible);
        void Focus(MenuFocus target);
    }
}
