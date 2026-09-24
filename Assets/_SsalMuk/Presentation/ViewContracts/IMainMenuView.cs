using System;
using SsalMuk.Core;

namespace SsalMuk.Presentation
{
    public interface IMainMenuView
    {
        event Action StartRequested;
        WeaponKind SelectedWeapon { get; }
        void Show(bool visible, bool canStart, string message);
    }
}
