using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SsalMuk.Core;
using SsalMuk.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SsalMuk.Tests
{
    public sealed class MultiWeaponCombatTests
    {
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (AppRoot.Instance != null) UnityEngine.Object.Destroy(AppRoot.Instance.gameObject);
            yield return null; yield return null;
        }
        [UnityTest]
        public IEnumerator FourOwnedWeaponsLaunchAndDealDamageInTheActualBattleLoop()
        {
            yield return SceneManager.LoadSceneAsync(AppRoot.MainMenuScenePath); yield return null;
            var app = AppRoot.Instance; app.Menu.StartButton.onClick.Invoke();
            double deadline = Time.realtimeSinceStartupAsDouble + 20;
            while (app.Coordinator.Phase != RunPhase.Running && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(app.Coordinator.Phase, Is.EqualTo(RunPhase.Running));
            var run = app.Coordinator.Run; var growth = new GrowthService(run);
            foreach (WeaponKind kind in Enum.GetValues(typeof(WeaponKind)))
            {
                if (kind != WeaponKind.Sword) growth.Equip(kind);
                growth.Upgrade(kind, UpgradeKind.Copies); growth.Upgrade(kind, UpgradeKind.Repeats); growth.Upgrade(kind, UpgradeKind.Speed, 10);
            }
            var simulation = UnityEngine.Object.FindAnyObjectByType<BattleRunner>().Simulation;
            Assert.That(simulation.Weapons.Count, Is.EqualTo(4));
            var hitKinds = new HashSet<WeaponKind>(); var keys = new Dictionary<long, WeaponKind>();
            foreach (var pair in simulation.Weapons) { var kind = pair.Key; pair.Value.Launched += shot => keys[shot.Key.AttackId] = kind; }
            simulation.DamageAccepted += hit => { if (keys.TryGetValue(hit.Request.Key.AttackId, out var kind)) hitKinds.Add(kind); };
            // Observe the real model hit sequence rather than accepting only visual launch events.
            var contacts = new List<UnitModel>();
            foreach (var offset in new[] { new DVec2(1.8, 0), new DVec2(0, 1.8), new DVec2(-1.8, 0), new DVec2(0, -1.8) })
            {
                var definition = new UnitDefinition("multi-target", UnitKind.Normal, 10000, 1.5, 0.26, 5, 1);
                contacts.Add(new GroundEnemyFactory(run.World.Units).Spawn(new UnitSpawnRequest(run.Id, UnitKind.Normal, run.Player.Position.Offset(offset), definition)));
            }
            deadline = Time.realtimeSinceStartupAsDouble + 4;
            while (Time.realtimeSinceStartupAsDouble < deadline && run.Phase == RunPhase.Running) yield return null;
            foreach (WeaponKind kind in Enum.GetValues(typeof(WeaponKind)))
            {
                Assert.That(simulation.Weapons[kind].LaunchCount, Is.GreaterThan(6), kind.ToString());
                Assert.That(keys.Values, Does.Contain(kind));
                Assert.That(hitKinds, Does.Contain(kind), "Each weapon must cause accepted damage.");
            }
            Assert.That(contacts.Sum(x => 10000 - x.Health), Is.GreaterThan(30));
            Assert.That(contacts.Sum(x => x.HitSequence), Is.GreaterThan(4)); Assert.That(run.Clock.ElapsedSeconds, Is.GreaterThan(2));
        }
    }
}
