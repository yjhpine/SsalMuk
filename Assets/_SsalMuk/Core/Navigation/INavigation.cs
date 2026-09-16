namespace SsalMuk.Core
{
    public interface INavigation
    {
        PathRequest RequestPath(WorldPosition from, WorldPosition to, double radius);
    }
}
