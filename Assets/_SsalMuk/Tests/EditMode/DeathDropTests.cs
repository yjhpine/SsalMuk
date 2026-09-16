using System.Linq;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class DeathDropTests
    {
        [Test]
        public void SimultaneousLethalHitsQueueOneOffscreenDeathAndOneDrop()
        {
            using var rig = RunTestRig.Create(); long enemy = rig.Spawn(UnitKind.Normal, new DVec2(150, -36));
            var position = rig.Unit(enemy).Position;
            Assert.That(rig.Hit(enemy, 100), Is.True); Assert.That(rig.Hit(enemy, 100), Is.False);
            Assert.That(rig.World.Units.TryGet(enemy, out _), Is.True, "Removal must wait for the phase boundary.");
            rig.Death.Flush(); rig.Death.Flush();
            Assert.That(rig.World.Units.TryGet(enemy, out _), Is.False); Assert.That(rig.Run.Kills, Is.EqualTo(1));
            Assert.That(rig.World.Experience.Count, Is.EqualTo(1));
            Assert.That(rig.World.Experience.Single().Position, Is.EqualTo(position));
            Assert.That(rig.World.Experience.Single().Value.ToString(), Is.EqualTo("1"));
        }

        [Test]
        public void DeadTargetIsReplacedBeforeAnotherAttack()
        {
            using var rig = RunTestRig.Create(); long first = rig.Spawn(UnitKind.Normal, new DVec2(2, 0));
            long second = rig.Spawn(UnitKind.Air, new DVec2(3, 0));
            rig.Hit(first, 100);
            Assert.That(TargetResolver.Resolve(rig.Player, rig.World.Query), Is.EqualTo(second));
        }
    }
}
