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
    }
}
