using System;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using SsalMuk.Core;

namespace SsalMuk.Tests
{
    public sealed class UnitFactoryTests
    {
        private static UnitDefinition Definition(UnitKind kind, string id = null) =>
            new UnitDefinition(id ?? kind.ToString(), kind, kind == UnitKind.Boss ? 600 : 100,
                2, kind == UnitKind.Boss ? 0.75 : 0.28, kind == UnitKind.Player ? 0 : 5,
                kind == UnitKind.Player ? BigInteger.Zero : BigInteger.One);

        private static UnitSpawnRequest Request(UnitRegistry registry, UnitKind kind, UnitDefinition definition = null, double x = 0) =>
            new UnitSpawnRequest(registry.RunId, kind, WorldPosition.FromLocal(new DVec2(x, 0)), definition ?? Definition(kind));

        [Test]
        public void UnitsShareDefinitionsButHaveSeparateIdentityAndPosition()
        {
            var registry = new UnitRegistry(Guid.NewGuid());
            var factory = new GroundEnemyFactory(registry);
            var definition = Definition(UnitKind.Normal);
            var a = factory.Spawn(Request(registry, UnitKind.Normal, definition, 2));
            var b = factory.Spawn(Request(registry, UnitKind.Normal, definition, 7));
            Assert.That(a.Definition, Is.SameAs(b.Definition));
            Assert.That(a.Id, Is.GreaterThan(0));
            Assert.That(b.Id, Is.Not.EqualTo(a.Id));
            Assert.That(a.Position.DistanceTo(b.Position), Is.EqualTo(5));
            Assert.That(a.Health, Is.EqualTo(definition.MaxHealth));
            Assert.That(b.Health, Is.EqualTo(definition.MaxHealth));
            Assert.That(registry.Get(a.Id), Is.SameAs(a));
        }

        [Test]
        public void PlayerStartsAtLevelOneWithOnlyItsOwnSwordState()
        {
            var first = new UnitRegistry(Guid.NewGuid());
            var second = new UnitRegistry(Guid.NewGuid());
            var a = (PlayerModel)new PlayerFactory(first).Spawn(Request(first, UnitKind.Player));
            var b = (PlayerModel)new PlayerFactory(second).Spawn(Request(second, UnitKind.Player));
            Assert.That(a.Level, Is.EqualTo(BigInteger.One));
            CollectionAssert.AreEqual(new[] { WeaponKind.Sword }, a.Weapons.Kinds);
            Assert.That(a.Weapons.Get(WeaponKind.Sword), Is.Not.SameAs(b.Weapons.Get(WeaponKind.Sword)));
            Assert.That(a.RunId, Is.Not.EqualTo(b.RunId));
        }

        [Test]
        public void ConcreteFactoriesCreateTheExpectedModelsIncludingBoss()
        {
            var registry = new UnitRegistry(Guid.NewGuid());
            var ground = new GroundEnemyFactory(registry);
            var normal = ground.Spawn(Request(registry, UnitKind.Normal));
            var boss = ground.Spawn(Request(registry, UnitKind.Boss));
            var air = new AirEnemyFactory(registry).Spawn(Request(registry, UnitKind.Air));
            Assert.That(normal, Is.TypeOf<GroundEnemyModel>());
            Assert.That(boss, Is.TypeOf<GroundEnemyModel>());
            Assert.That(air, Is.TypeOf<AirEnemyModel>());
            Assert.That(boss.Health, Is.GreaterThan(normal.Health));
            Assert.That(boss.BodyRadius, Is.GreaterThan(normal.BodyRadius));
            Assert.That(registry.Count, Is.EqualTo(3));
        }

        [Test]
        public void ForeignRunAndWrongKindRequestsDoNotRegisterAnything()
        {
            var registry = new UnitRegistry(Guid.NewGuid());
            var factory = new PlayerFactory(registry);
            var foreign = new UnitSpawnRequest(Guid.NewGuid(), UnitKind.Player, default, Definition(UnitKind.Player));
            Assert.Throws<ArgumentException>(() => factory.Spawn(foreign));
            Assert.Throws<ArgumentException>(() => factory.Spawn(Request(registry, UnitKind.Normal)));
            Assert.Throws<ArgumentException>(() => factory.Spawn(Request(registry, UnitKind.Player, Definition(UnitKind.Boss))));
            Assert.That(registry.Count, Is.Zero);
            Assert.That(factory.Spawn(Request(registry, UnitKind.Player)).Id, Is.EqualTo(1));
        }

        [Test]
        public void RemovingAUnitDoesNotReuseItsId()
        {
            var registry = new UnitRegistry(Guid.NewGuid());
            var factory = new AirEnemyFactory(registry);
            var first = factory.Spawn(Request(registry, UnitKind.Air));
            Assert.That(registry.Remove(first.Id), Is.True);
            Assert.That(registry.TryGet(first.Id, out _), Is.False);
            var next = factory.Spawn(Request(registry, UnitKind.Air));
            Assert.That(next.Id, Is.GreaterThan(first.Id));
        }

        [Test]
        public void DefinitionCatalogRequiresEveryKindAndUniqueIds()
        {
            var definitions = Enum.GetValues(typeof(UnitKind)).Cast<UnitKind>().Select(x => Definition(x)).ToArray();
            var catalog = new DefinitionCatalog(definitions);
            Assert.That(catalog.GetUnit(UnitKind.Player), Is.SameAs(definitions[0]));
            Assert.Throws<ArgumentException>(() => new DefinitionCatalog(definitions.Take(3)));
            Assert.Throws<ArgumentException>(() => new DefinitionCatalog(definitions.Concat(new[] { definitions[0] })));
            definitions[1] = Definition(UnitKind.Normal, definitions[0].Id);
            Assert.Throws<ArgumentException>(() => new DefinitionCatalog(definitions));
        }

        [TestCase(0, 1, 0.2)] [TestCase(1, 0, 0.2)] [TestCase(1, 1, 0)]
        [TestCase(double.NaN, 1, 0.2)] [TestCase(1, double.PositiveInfinity, 0.2)]
        public void InvalidUnitStatsAreRejected(double health, double speed, double radius)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new UnitDefinition("normal", UnitKind.Normal, health, speed, radius, 1, BigInteger.One));
        }
    }
}
