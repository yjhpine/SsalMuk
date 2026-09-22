namespace SsalMuk.Core
{
    public interface IPlayerPosition
    {
        bool IsAlive { get; }
        WorldPosition Position { get; }
        double MoveSpeed { get; }
    }

    // One snapshot per movement step, shared by every enemy in this run.
    public sealed class SharedPlayerPosition : IPlayerPosition
    {
        public bool IsAlive { get; private set; }
        public WorldPosition Position { get; private set; }
        public double MoveSpeed { get; private set; }
        public void Refresh(PlayerModel player)
        { IsAlive = player.IsAlive; Position = player.Position; MoveSpeed = player.Definition.MoveSpeed; }
    }
}
