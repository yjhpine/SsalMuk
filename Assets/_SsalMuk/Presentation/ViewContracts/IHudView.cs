namespace SsalMuk.Presentation
{
    public interface IHudView
    {
        void Show(bool visible, string health, double healthFraction, string level, string experience, double experienceFraction, string survival, string kills);
    }
}
