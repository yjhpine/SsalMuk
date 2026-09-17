using System;
namespace SsalMuk.Core
{
    public readonly struct AttackShapeSnapshot
    {
        public HitKey Key { get; }
        public WeaponKind Kind { get; }
        public WorldPosition Origin { get; }
        public DVec2 Direction { get; }
        public double Progress { get; }
        public double Range { get; }
        public double Width { get; }
        public double AngleRadians { get; }
        public DVec2 Tip => new DVec2(Math.Cos(AngleRadians), Math.Sin(AngleRadians)) * (Kind == WeaponKind.Spear ? Range * Progress : Range);
        public AttackShapeSnapshot(AttackInstance attack, WeaponDefinition definition)
        {
            if (attack == null || definition == null || attack.Kind != definition.Kind) throw new ArgumentException("The visual shape needs the matching combat definition.");
            Key = attack.Key; Kind = attack.Kind; Origin = attack.Origin; Direction = attack.Direction; Progress = attack.Progress;
            Range = attack.Stats.Range; Width = definition.Width;
            AngleRadians = Math.Atan2(Direction.Y, Direction.X) + (Kind == WeaponKind.Sword ? -Math.PI / 6 + Progress * Math.PI / 3 :
                Kind == WeaponKind.Axe ? Progress * Math.PI * 2 : 0);
        }
    }
}
