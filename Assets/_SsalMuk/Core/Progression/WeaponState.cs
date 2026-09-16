namespace SsalMuk.Core
{
    public sealed class WeaponState
    {
        public WeaponKind Kind { get; }
        internal WeaponState(WeaponKind kind) { Kind = kind; }
    }
}
