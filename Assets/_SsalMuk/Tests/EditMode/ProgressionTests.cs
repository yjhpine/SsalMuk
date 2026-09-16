using System.Numerics;
using System.Threading.Tasks;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class ProgressionTests
    {
        [Test]
        public void ExperiencePaysEveryCostAndAccumulatesAllPendingChoices()
        {
            using var rig = RunTestRig.Create();
            Assert.That(rig.GrantExperience(100), Is.True);
            Assert.That(rig.Player.Level, Is.EqualTo(new BigInteger(8)));
            Assert.That(rig.Player.Growth.ExperienceIntoLevel, Is.EqualTo(new BigInteger(2)));
            Assert.That(rig.Player.Growth.PendingChoices, Is.EqualTo(new BigInteger(7)));
            rig.GrantExperience(24);
            Assert.That(rig.Player.Level, Is.EqualTo(new BigInteger(9)));
            Assert.That(rig.Player.Growth.ExperienceIntoLevel, Is.EqualTo(BigInteger.Zero));
            Assert.That(rig.Player.Growth.PendingChoices, Is.EqualTo(new BigInteger(8)));
        }
        [Test, Timeout(2000)]
        public void HugeExperienceUsesExactArithmeticRatherThanALoopPerLevel()
        {
            using var rig = RunTestRig.Create(); BigInteger count = BigInteger.Pow(10, 40);
            BigInteger cost = 5 * count + 3 * count * (count - 1) / 2;
            rig.GrantExperience(cost + 2);
            Assert.That(rig.Player.Level, Is.EqualTo(count + 1));
            Assert.That(rig.Player.Growth.ExperienceIntoLevel, Is.EqualTo(new BigInteger(2)));
            Assert.That(rig.Player.Growth.PendingChoices, Is.EqualTo(count));
            Assert.That(NumberFormatter.Format(rig.Player.Level), Is.EqualTo("1.00e40"));
        }
        [Test]
        public void GrowthCostUsesTheProvidedBalanceCoefficients()
        {
            var settings = new GrowthSettings(firstCost: 7, costStep: 2);
            Assert.That(settings.CostForLevels(1, 3), Is.EqualTo(new BigInteger(27)));
            Assert.That(settings.CostForLevels(4, 2), Is.EqualTo(new BigInteger(28)));
        }
        [Test]
        public async Task DeathResetsGrowthAfterCopyingTheResultAndRestartUsesNewState()
        {
            using var rig = RunTestRig.Create(enableAi: false);
            rig.GrantExperience(100); rig.Equip(WeaponKind.Axe); rig.Upgrade(WeaponKind.Sword, UpgradeKind.Damage);
            var oldRun = rig.Run; var oldPlayer = rig.Player; var growth = rig.Player.Growth; var sword = rig.Player.Weapons.Get(WeaponKind.Sword);
            var oldProgression = new ProgressionService(rig.Run);
            long staleOrb = rig.DropXp(new DVec2(1, 0), 25); rig.World.TryBeginAttraction(oldRun.Id, staleOrb);
            rig.Hit(rig.Player.Id, 1000); rig.Advance(0.02);
            Assert.That(rig.Result.FinalLevel, Is.EqualTo(new BigInteger(8)));
            Assert.That(oldPlayer.Growth.PendingChoices, Is.EqualTo(BigInteger.Zero));
            Assert.That(oldPlayer.Level, Is.EqualTo(BigInteger.One)); Assert.That(oldPlayer.Weapons.Kinds, Is.EquivalentTo(new[] { WeaponKind.Sword }));
            Assert.That(oldPlayer.Weapons.Get(WeaponKind.Sword).GetLevel(UpgradeKind.Damage), Is.EqualTo(BigInteger.Zero));
            Assert.That(oldProgression.AddExperience(100), Is.False);
            await rig.RestartAsync();
            Assert.That(rig.Player.Growth, Is.Not.SameAs(growth)); Assert.That(rig.Player.Weapons.Get(WeaponKind.Sword), Is.Not.SameAs(sword));
            Assert.That(rig.Player.Growth.PendingChoices, Is.EqualTo(BigInteger.Zero)); Assert.That(rig.Player.Level, Is.EqualTo(BigInteger.One));
            long newOrb = rig.DropXp(new DVec2(1, 0), 5); rig.World.TryBeginAttraction(rig.Run.Id, newOrb);
            Assert.That(rig.World.TryCollectExperience(oldRun.Id, newOrb, out _), Is.False);
            Assert.That(oldRun.World.TryCollectExperience(oldRun.Id, staleOrb, out _), Is.False);
        }
    }
}
