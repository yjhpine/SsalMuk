using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class TargetResolverTests
    {
        [Test]
        public void NearestIncludesOffscreenChunksAndBreaksDistanceTiesByIdentity()
        {
            using var rig = RunTestRig.Create();
            long first = rig.Spawn(UnitKind.Air, new DVec2(100, 0));
            rig.Spawn(UnitKind.Boss, new DVec2(-100, 0));
            Assert.That(TargetResolver.Resolve(rig.Player, rig.World.Query), Is.EqualTo(first));
            rig.World.Units.Remove(first);
            Assert.That(TargetResolver.Resolve(rig.Player, rig.World.Query), Is.EqualTo(3));
        }

        [Test]
        public void EmptyWorldHasNoTarget()
        {
            using var rig = RunTestRig.Create();
            Assert.That(TargetResolver.Resolve(rig.Player, rig.World.Query), Is.Null);
        }

        [Test]
        public void NearbyBossOutweighsAFartherScoredNormalButNotAnImmediateThreat()
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            long normal = rig.Spawn(UnitKind.Normal, new DVec2(-3, 0));
            long boss = rig.Spawn(UnitKind.Boss, new DVec2(6, 0));
            Assert.That(TargetResolver.Resolve(rig.Player, rig.World.Query), Is.EqualTo(boss));
            rig.World.MoveUnit(normal, WorldPosition.FromLocal(new DVec2(-1, 0)));
            Assert.That(TargetResolver.Resolve(rig.Player, rig.World.Query), Is.EqualTo(normal));
        }

        [Test]
        public void BossOutsideTheNearbySearchDoesNotDisplaceALocalEnemy()
        {
            using var rig = RunTestRig.Create(enableAi: false, enableCombat: false);
            long normal = rig.Spawn(UnitKind.Normal, new DVec2(-10, 0));
            rig.Spawn(UnitKind.Boss, new DVec2(20, 0));
            Assert.That(TargetResolver.Resolve(rig.Player, rig.World.Query), Is.EqualTo(normal));
        }
    }
}
