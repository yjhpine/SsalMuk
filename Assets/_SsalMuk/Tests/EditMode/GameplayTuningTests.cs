using System;
using System.Collections.Generic;
using NUnit.Framework;
using SsalMuk.Core;
using SsalMuk.Unity;
using UnityEngine;

namespace SsalMuk.Tests
{
    public sealed class GameplayTuningTests
    {
        [TestCase(WeaponKind.Sword)]
        [TestCase(WeaponKind.Spear)]
        [TestCase(WeaponKind.Axe)]
        [TestCase(WeaponKind.Fireball)]
        public void CopiesFanOutFifteenDegreesPerPairFromThePlayer(WeaponKind kind)
        {
            using var rig = RunTestRig.Create(enableAi: false);
            if (kind != WeaponKind.Sword) rig.Equip(kind);
            rig.Upgrade(kind, UpgradeKind.Copies, 4);
            rig.Spawn(UnitKind.Normal, new DVec2(8, 0), 1000);
            using var weapon = new WeaponRuntime(rig.Run, kind, rig.Movement, rig.Damage);
            var shots = new List<AttackInstance>();
            weapon.Launched += shot =>
            {
                shots.Add(shot);
                Assert.That(shot.Origin, Is.EqualTo(rig.Player.Position));
                double degrees = new[] { 0, 15, -15, 30, -30 }[shots.Count - 1];
                Assert.That(Math.Atan2(shot.Direction.Y, shot.Direction.X), Is.EqualTo(degrees * Math.PI / 180).Within(1e-10));
                var shape = new AttackShapeSnapshot(shot, rig.Run.Definitions.GetWeapon(kind));
                double sweepStart = kind == WeaponKind.Sword ? -Math.PI / 6 : 0;
                Assert.That(shape.AngleRadians, Is.EqualTo(degrees * Math.PI / 180 + sweepStart).Within(1e-10));
            };
            weapon.Tick(0, .02);
            Assert.That(shots.Count, Is.EqualTo(5));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void StarterAiClosesToSwordRangeAndActuallyHitsWithoutLoot(bool approachingEnemy)
        {
            var catalog = Resources.Load<GameCatalog>("Bootstrap/GameCatalog");
            using var rig = RunTestRig.Create(definitions: catalog.CreateDefinitions(), pickupSettings: catalog.Defaults.CreatePickupSettings());
            long enemy = rig.Spawn(UnitKind.Normal, new DVec2(4, 0), 100);
            if (!approachingEnemy) rig.Movement.SetMoveIntent(enemy, DVec2.Zero);
            rig.Advance(3);
            Assert.That(rig.Unit(enemy).Health, Is.LessThan(100), "AI must enter attack range instead of avoiding forever.");
            Assert.That(rig.Player.Health, Is.EqualTo(100), "A single ordinary enemy should not require deliberate contact.");
        }

        [Test]
        public void CameraMarginRetiresMissedShotsBeforeTheyCanHitDistantTargets()
        {
            using var rig = RunTestRig.Create(enableAi: false); rig.Equip(WeaponKind.Fireball);
            long enemy = rig.Spawn(UnitKind.Normal, new DVec2(8, 0), 100);
            rig.Movement.SetMoveIntent(enemy, DVec2.Zero);
            rig.Simulation.SetViewBounds(new WorldRect(rig.Player.Position, 2, 2));
            int explosions = 0;
            rig.Simulation.Weapons[WeaponKind.Fireball].Projectiles.Exploded += _ => explosions++;
            for (int i = 0; i < 60; i++) rig.Simulation.Step(.02);
            Assert.That(rig.Unit(enemy).Health, Is.EqualTo(100));
            Assert.That(rig.Simulation.Projectiles, Is.Empty);
            Assert.That(explosions, Is.Zero);
            Assert.That(rig.Damage.CachedHitCount, Is.Zero);
        }

        [Test]
        public void CameraMovingAwayRetiresExistingShotsWithoutExplosion()
        {
            using var rig = RunTestRig.Create(enableAi: false); rig.Equip(WeaponKind.Fireball);
            long enemy = rig.Spawn(UnitKind.Normal, new DVec2(8, 0), 100);
            rig.Movement.SetMoveIntent(enemy, DVec2.Zero);
            rig.Simulation.SetViewBounds(new WorldRect(rig.Player.Position, 2, 2));
            rig.Simulation.Step(.02);
            Assert.That(rig.Simulation.Projectiles.Count, Is.EqualTo(1));
            var shot = rig.Simulation.Projectiles[0];
            rig.Simulation.SetViewBounds(new WorldRect(WorldPosition.FromLocal(new DVec2(100, 100)), 2, 2));
            rig.Simulation.Step(.02);
            Assert.That(shot.IsAlive, Is.False);
            Assert.That(rig.Simulation.Projectiles, Is.Empty);
            Assert.That(rig.Simulation.Explosions, Is.Empty);
        }

        [Test]
        public void ActualDefaultsUseRequestedBossSpeedMonsterBodiesAndPickupRadius()
        {
            var catalog = Resources.Load<GameCatalog>("Bootstrap/GameCatalog");
            var definitions = catalog.CreateDefinitions();
            Assert.That(definitions.GetUnit(UnitKind.Boss).MoveSpeed,
                Is.EqualTo(definitions.GetUnit(UnitKind.Player).MoveSpeed * 1.2).Within(1e-10));
            Assert.That(definitions.GetUnit(UnitKind.Normal).MoveSpeed,
                Is.EqualTo(definitions.GetUnit(UnitKind.Player).MoveSpeed * 1.1).Within(1e-10));
            Assert.That(definitions.GetUnit(UnitKind.Air).MoveSpeed,
                Is.EqualTo(definitions.GetUnit(UnitKind.Player).MoveSpeed * 1.1).Within(1e-10));
            Assert.That(catalog.Defaults.CreatePickupSettings().AttractionRadius, Is.EqualTo(1.95).Within(1e-10));
            foreach (UnitKind kind in Enum.GetValues(typeof(UnitKind)))
            {
                var definition = definitions.GetUnit(kind); var art = catalog.Visuals.Unit(kind);
                double diameter = Math.Min(art.Height, art.Height * art.Sprite.bounds.size.x / art.Sprite.bounds.size.y);
                Assert.That(definition.BodyRadius * 2, Is.EqualTo(diameter * (kind == UnitKind.Player ? .8 : 1)).Within(1e-6));
                Assert.That(definition.HurtRadius * 2, Is.EqualTo(diameter * 1.1).Within(1e-6));
            }
        }
    }
}
