using SsalMuk.Core;
namespace SsalMuk.Presentation
{
    public interface IChestWorldView
    {
        void ShowChest(ChestRecord chest, DVec2 relative);
        void ShowChestDirection(DVec2? target);
    }
}
