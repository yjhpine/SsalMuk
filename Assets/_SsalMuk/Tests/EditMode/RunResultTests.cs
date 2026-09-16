using System;
using System.Numerics;
using System.Threading.Tasks;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class RunResultTests
    {
        [Test]
        public void LethalDamageCopiesTheResultAndDisposesTheOldWorldOnce()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            long enemy = rig.Spawn(UnitKind.Normal, new DVec2(0.3, 0), 1); rig.Advance(0.2);
            var old = rig.Run; var player = rig.Player; double time = rig.Clock.ElapsedSeconds;
            Assert.That(rig.Hit(player.Id, 1000), Is.True); rig.Advance(0.02);
            Assert.That(rig.Result, Is.Not.Null);
            Assert.That(rig.Result.RunId, Is.EqualTo(old.Id)); Assert.That(rig.Result.KillCount, Is.EqualTo(1));
            Assert.That(rig.Result.SurvivalSeconds, Is.EqualTo(time)); Assert.That(rig.Result.FinalLevel, Is.EqualTo(BigInteger.One));
            Assert.That(old.Phase, Is.EqualTo(RunPhase.Disposed)); Assert.That(old.World.Units.Count, Is.Zero);
            var snapshot = rig.Result; rig.Advance(1);
            Assert.That(rig.Result, Is.SameAs(snapshot)); Assert.That(old.Clock.ElapsedSeconds, Is.EqualTo(time));
        }

        [Test]
        public async Task RestartLoadFailurePreservesTheResultAndAllowsMenuRetry()
        {
            var loader = new FailingLoader();
            using var rig = RunTestRig.Create(sceneLoader: loader);
            rig.Hit(rig.Player.Id, 1000); rig.Advance(0.02); var result = rig.Result;
            loader.Fail = true; await rig.RestartAsync();
            Assert.That(rig.Coordinator.Phase, Is.EqualTo(RunPhase.MainMenu));
            Assert.That(rig.Coordinator.Run, Is.Null); Assert.That(rig.Coordinator.LastError, Does.Contain("fixture"));
            Assert.That(rig.Result, Is.SameAs(result)); Assert.That(rig.Run.Phase, Is.EqualTo(RunPhase.Disposed));
            loader.Fail = false; await rig.Coordinator.StartRunAsync();
            Assert.That(rig.Coordinator.Phase, Is.EqualTo(RunPhase.Running)); Assert.That(rig.Result, Is.Null);
            Assert.That(rig.Run.Id, Is.Not.EqualTo(result.RunId));
        }

        private sealed class FailingLoader : ISceneLoader
        {
            public bool Fail;
            public Task LoadBattleAsync() => Fail ? Task.FromException(new InvalidOperationException("fixture scene failure")) : Task.CompletedTask;
            public Task LoadMainMenuAsync() => Task.CompletedTask;
        }

        [Test]
        public async Task RestartCreatesANewRunAndRejectsOldDamage()
        {
            using var rig = RunTestRig.Create(); var old = rig.Run; var oldDamage = rig.Damage;
            var stale = new DamageRequest(new HitKey(old.Id, old.AllocateAttackId(), 0, 0), rig.Player.Id, rig.Player.Id, 5, DVec2.Zero);
            rig.Hit(rig.Player.Id, 1000); rig.Advance(0.02); await rig.RestartAsync();
            Assert.That(rig.Run.Id, Is.Not.EqualTo(old.Id)); Assert.That(rig.Run.Seed, Is.Not.EqualTo(old.Seed));
            Assert.That(rig.Player.Weapons.Kinds, Is.EquivalentTo(new[] { WeaponKind.Sword }));
            Assert.That(rig.Player.Level, Is.EqualTo(BigInteger.One)); Assert.That(rig.Clock.ElapsedSeconds, Is.Zero);
            Assert.That(oldDamage.TryApply(stale, 1), Is.False); Assert.That(rig.Damage.TryApply(stale, 1), Is.False);
            Assert.That(rig.Player.Health, Is.EqualTo(100));
        }
    }
}
