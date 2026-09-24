using System;
using System.Linq;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class PowerupTests
    {
        [Test]
        public void GlobalMagnetPullsDistantChunksButAwardsOnlyAfterContact()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            long far = rig.DropXp(new DVec2(1000, 0), 2);
            long other = rig.DropXp(new DVec2(-500, 60), 2);
            rig.World.AddItem(PowerupKind.Magnet, rig.Player.Position);
            rig.Advance(.02);
            Assert.That(rig.World.Items, Is.Empty);
            Assert.That(rig.World.TryGetExperience(far, out var orb), Is.True);
            Assert.That(orb.State, Is.EqualTo(ExperienceState.Attracting));
            Assert.That(rig.Player.Growth.ExperienceIntoLevel, Is.EqualTo(System.Numerics.BigInteger.Zero));
            rig.Advance(2);
            Assert.That(rig.World.Experience, Is.Empty);
            Assert.That(rig.Player.Growth.ExperienceIntoLevel, Is.EqualTo(new System.Numerics.BigInteger(4)));
        }
        [Test]
        public void SpeedBoostRefreshesTenSecondsWithoutStackingAndMovementUsesIt()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            rig.World.AddItem(PowerupKind.MoveSpeed, rig.Player.Position); rig.Advance(.02);
            Assert.That(rig.Player.MoveSpeed, Is.EqualTo(4.5));
            var start = rig.Player.Position; rig.Movement.SetMoveIntent(rig.Player.Id, new DVec2(1, 0)); rig.Advance(1);
            Assert.That(start.DistanceTo(rig.Player.Position), Is.EqualTo(4.5).Within(1e-8));
            rig.Movement.SetMoveIntent(rig.Player.Id, DVec2.Zero); rig.Advance(4);
            rig.World.AddItem(PowerupKind.MoveSpeed, rig.Player.Position); rig.Advance(.02);
            Assert.That(rig.Player.MoveSpeed, Is.EqualTo(4.5));
            rig.Advance(9.98); Assert.That(rig.Player.MoveSpeed, Is.EqualTo(4.5));
            rig.Advance(.02); Assert.That(rig.Player.MoveSpeed, Is.EqualTo(3));
        }
        [Test]
        public void InvincibilityRejectsDamageUntilExactlyTenSecondsAndRefreshes()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            long enemy = rig.Spawn(UnitKind.Normal, new DVec2(20, 0), 1000);
            rig.World.AddItem(PowerupKind.Invincibility, rig.Player.Position); rig.Advance(.02);
            double until = rig.Player.Effects.InvincibilityUntil;
            Assert.That(until - rig.Clock.ElapsedSeconds, Is.EqualTo(10).Within(1e-10));
            bool Hit(double time) => rig.Damage.TryApply(new DamageRequest(new HitKey(rig.Run.Id, rig.Run.AllocateAttackId(), 0, 0),
                enemy, rig.Player.Id, 5, DVec2.Zero), time);
            Assert.That(Hit(until - 1e-6), Is.False);
            Assert.That(Hit(until), Is.True);
            rig.Advance(1);
            rig.World.AddItem(PowerupKind.Invincibility, rig.Player.Position); rig.Advance(.02);
            Assert.That(rig.Player.Effects.InvincibilityUntil, Is.EqualTo(rig.Clock.ElapsedSeconds + 10));
        }
        [TestCase(WeaponKind.Sword), TestCase(WeaponKind.Spear), TestCase(WeaponKind.Axe), TestCase(WeaponKind.Fireball)]
        public void RangeItemMultipliesCurrentUpgradedRangeAndThenExpires(WeaponKind kind)
        {
            using var rig = RunTestRig.Create(enableAi: false);
            if (kind != WeaponKind.Sword) rig.Equip(kind);
            rig.Upgrade(kind, UpgradeKind.Range, 2);
            double ordinary = StatCalculator.Calculate(rig.Run.Definitions.GetWeapon(kind), rig.Player.Weapons.Get(kind), rig.Run.GrowthSettings).Range;
            rig.Spawn(UnitKind.Normal, new DVec2(80, 0), 100000);
            double observed = 0;
            rig.Simulation.Weapons[kind].Launched += shot => observed = shot.Stats.Range;
            rig.World.AddItem(PowerupKind.AttackRange, rig.Player.Position);
            rig.Advance(.02);
            Assert.That(observed, Is.EqualTo(ordinary * 10).Within(1e-8));
            rig.Advance(10.02);
            rig.Advance(2);
            Assert.That(observed, Is.EqualTo(ordinary).Within(1e-8));
        }
        [Test]
        public void ActualDeathsDropAtTheThresholdAndRestartClearsItemsAndEffects()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            using (var drops = new PowerupSystem(rig.Run, rig.Movement, rig.Death, new PowerupSettings(.02), new FixedRandom(.019999)))
            {
                long enemy = rig.Spawn(UnitKind.Normal, new DVec2(10, 0), 1);
                rig.Hit(enemy, 10); rig.Death.Flush();
                Assert.That(rig.World.Items.Any(item => item.Kind == PowerupKind.Magnet), Is.True);
            }
            rig.World.AddItem(PowerupKind.MoveSpeed, rig.Player.Position); rig.Advance(.02);
            rig.Hit(rig.Player.Id, 1000); rig.Advance(.02);
            Assert.That(rig.Player.MoveSpeed, Is.EqualTo(3));
            rig.RestartAsync().GetAwaiter().GetResult();
            Assert.That(rig.World.Items, Is.Empty);
            Assert.That(rig.Player.Effects.RangeMultiplier, Is.EqualTo(1));
        }
        [Test]
        public void RunningSimulationDropsItemsWithoutInstallingAnExtraDropService()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            for (int i = 0; i < 200; i++)
            {
                long enemy = rig.Spawn(UnitKind.Normal, new DVec2(10, 0), 1);
                rig.Hit(enemy, 10);
            }
            rig.Advance(.02);
            Assert.That(rig.Run.Kills, Is.EqualTo(200));
            Assert.That(rig.World.Items.Count, Is.InRange(1, 20));
            Assert.That(rig.World.Items.All(item => Enum.IsDefined(typeof(PowerupKind), item.Kind)), Is.True);
        }
        private sealed class FixedRandom : IRandomSource
        {
            private readonly double value;
            public FixedRandom(double value) => this.value = value;
            public double NextUnit() => value;
        }
    }
}
