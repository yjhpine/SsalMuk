using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class FireballTests
    {
        [Test]
        public void FirstContactExplodesOnceAndDealsOnlySixDamageToEachOverlappingTarget()
        {
            using var rig = RunTestRig.Create(enableAi: false); rig.Equip(WeaponKind.Fireball);
            long first = rig.Spawn(UnitKind.Normal, new DVec2(2, 0), 20);
            long second = rig.Spawn(UnitKind.Air, new DVec2(2, 0.5), 20);
            long outside = rig.Spawn(UnitKind.Normal, new DVec2(2, 2), 20);
            using var system = new ProjectileSystem(rig.Run, rig.Movement, rig.Damage); var explosions = new List<ExplosionSnapshot>(); system.Exploded += explosions.Add;
            var projectile = new FireballAttack(system).Launch(Create(rig)); system.Step(0, 1);
            Assert.That(explosions.Count, Is.EqualTo(1)); Assert.That(explosions[0].FirstHitTargetId, Is.EqualTo(first));
            Assert.That(explosions[0].Position.Local.X, Is.EqualTo(1.64).Within(1e-8));
            Assert.That(rig.Unit(first).Health, Is.EqualTo(14)); Assert.That(rig.Unit(second).Health, Is.EqualTo(14));
            Assert.That(rig.Unit(outside).Health, Is.EqualTo(20)); Assert.That(projectile.IsAlive, Is.False); Assert.That(system.Active, Is.Empty);
            system.Step(1, 2); Assert.That(explosions.Count, Is.EqualTo(1)); Assert.That(rig.Damage.CachedHitCount, Is.Zero);
        }
        [Test]
        public void RelativeMotionCatchesAFastCrossingAirEnemy()
        {
            using var rig = RunTestRig.Create(enableAi: false); rig.Equip(WeaponKind.Fireball);
            long enemy = rig.Spawn(UnitKind.Air, new DVec2(4, -3), 20); rig.Movement.SetAirDirection(enemy, new DVec2(0, 1));
            using var system = new ProjectileSystem(rig.Run, rig.Movement, rig.Damage); var shot = system.Launch(Create(rig));
            rig.Movement.Step(1); system.Step(0, 1);
            Assert.That(rig.Unit(enemy).Health, Is.EqualTo(14)); Assert.That(shot.HitTargetId, Is.EqualTo(enemy));
        }
        [Test]
        public void FireballIgnoresSolidTerrainAndHitsAllTargetsAtTheSamePosition()
        {
            using var rig = RunTestRig.Create(enableAi: false, terrain: new Wall()); rig.Equip(WeaponKind.Fireball);
            var targets = Enumerable.Range(0, 3).Select(_ => rig.Spawn(UnitKind.Normal, new DVec2(3, 0), 20)).ToArray();
            using var system = new ProjectileSystem(rig.Run, rig.Movement, rig.Damage); var shot = system.Launch(Create(rig)); system.Step(0, 1);
            foreach (long id in targets) Assert.That(rig.Unit(id).Health, Is.EqualTo(14));
            Assert.That(shot.HitTargetId, Is.EqualTo(targets[0]));
        }
        [Test]
        public void OffscreenProjectilesRemainUntilTheirFlightLifetimeAndDoNotExplodeOnExpiry()
        {
            using var rig = RunTestRig.Create(enableAi: false); rig.Equip(WeaponKind.Fireball);
            using var system = new ProjectileSystem(rig.Run, rig.Movement, rig.Damage); int explosions = 0; system.Exploded += _ => explosions++;
            var shot = system.Launch(Create(rig)); system.Step(0, 2);
            Assert.That(system.Active.Count, Is.EqualTo(1)); Assert.That(shot.Position.DistanceTo(rig.Player.Position), Is.EqualTo(16));
            system.Step(2, 3); Assert.That(system.Active, Is.Empty); Assert.That(shot.IsAlive, Is.False); Assert.That(explosions, Is.Zero);
        }
        [Test]
        public void LaunchedProjectileKeepsItsStatsAndNewlyLaunchedShotsDoNotHitPastMotion()
        {
            using var rig = RunTestRig.Create(enableAi: false); rig.Equip(WeaponKind.Fireball);
            long target = rig.Spawn(UnitKind.Air, new DVec2(2, 0), 20);
            using var system = new ProjectileSystem(rig.Run, rig.Movement, rig.Damage); var shot = system.Launch(Create(rig));
            Assert.That(shot, Is.Not.Null); rig.Upgrade(WeaponKind.Fireball, UpgradeKind.Damage, 10); rig.Upgrade(WeaponKind.Fireball, UpgradeKind.Range, 10);
            system.Step(0, 1); Assert.That(rig.Unit(target).Health, Is.EqualTo(14)); Assert.That(shot.Stats.Range, Is.EqualTo(0.8));
            rig.World.MoveUnit(target, WorldPosition.FromLocal(new DVec2(2, 0))); rig.Movement.SetAirDirection(target, new DVec2(-1, 0)); rig.Movement.Step(1);
            var late = system.Launch(Create(rig, 1)); system.Step(0, 1);
            Assert.That(late.Position, Is.EqualTo(rig.Player.Position)); Assert.That(late.IsAlive, Is.True);
            Assert.That(late.Stats.Damage, Is.EqualTo(12)); Assert.That(rig.Unit(target).Health, Is.EqualTo(14));
        }
        private static AttackInstance Create(RunTestRig rig, double time = 0)
        {
            var definition = rig.Run.Definitions.GetWeapon(WeaponKind.Fireball);
            return new AttackInstance(new HitKey(rig.Run.Id, rig.Run.AllocateAttackId(), 0, 0), WeaponKind.Fireball, time,
                definition.ActiveSeconds, new DVec2(1, 0), StatCalculator.Calculate(definition, rig.Player.Weapons.Get(WeaponKind.Fireball)), rig.Player.Position);
        }
        [Test]
        public void FireballRangeUpgradeExpandsTheActualExplosionOnTheNextLaunch()
        {
            using var rig = RunTestRig.Create(enableAi: false); rig.Equip(WeaponKind.Fireball);
            long target = rig.Spawn(UnitKind.Normal, new DVec2(2, 0), 20);
            long outer = rig.Spawn(UnitKind.Normal, new DVec2(2.5, 1.3), 20);
            using var runtime = new WeaponRuntime(rig.Run, WeaponKind.Fireball, rig.Movement, rig.Damage);
            runtime.Tick(0, 0.6); Assert.That(rig.Unit(target).Health, Is.EqualTo(14)); Assert.That(rig.Unit(outer).Health, Is.EqualTo(20));
            rig.Upgrade(WeaponKind.Fireball, UpgradeKind.Damage, 10); rig.Upgrade(WeaponKind.Fireball, UpgradeKind.Range, 10);
            runtime.Tick(0.6, 2);
            Assert.That(rig.Unit(target).Health, Is.EqualTo(2)); Assert.That(rig.Unit(outer).Health, Is.EqualTo(8));
        }
        private sealed class Wall : IChunkGenerator
        {
            public ChunkData Generate(ChunkCoord coord)
            {
                var blocks = new bool[1024]; if (coord.Equals(default(ChunkCoord))) for (int y = 0; y < 32; y++) blocks[y * 32 + 1] = true;
                return new ChunkData(coord, blocks, 1);
            }
        }
    }
}
