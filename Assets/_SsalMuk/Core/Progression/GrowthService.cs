using System;
using System.Numerics;

namespace SsalMuk.Core
{
    public sealed class GrowthService
    {
        private readonly RunModel run;
        public GrowthService(RunModel run) => this.run = run ?? throw new ArgumentNullException(nameof(run));
        public void Equip(WeaponKind kind) { RequireActive(); run.Player.Weapons.Equip(kind); }
        public void Upgrade(WeaponKind kind, UpgradeKind upgrade, BigInteger? count = null)
        {
            RequireActive(); BigInteger amount = count ?? BigInteger.One;
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (!run.Player.Weapons.Owns(kind)) throw new InvalidOperationException("Only an owned weapon can be upgraded.");
            var state = run.Player.Weapons.Get(kind); BigInteger next = state.GetLevel(upgrade) + amount;
            StatCalculator.Calculate(run.Definitions.GetWeapon(kind), state, run.GrowthSettings, upgrade, next);
            state.SetLevel(upgrade, next);
        }
        private void RequireActive()
        {
            if (run.Phase != RunPhase.Running || run.Player == null || !run.Player.IsAlive)
                throw new InvalidOperationException("Growth requires a living player in the current run.");
        }
    }
}
