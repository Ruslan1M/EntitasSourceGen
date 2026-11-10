using Entitas;
using NUnit.Framework;

namespace Sample.Tests
{
    public class EntityIndexApiTests
    {
        Contexts _contexts;

        [SetUp]
        public void SetUp() => _contexts = new Contexts();

        [TearDown]
        public void TearDown() => _contexts.Reset();

        [Test]
        public void Primary_FindAfterAddReplaceRemove()
        {
            var game = _contexts.game;
            var e = game.CreateEntity();
            e.AddPlayerIndex(1);
            Assert.AreSame(e, game.GetEntityWithPlayerIndex(1));

            e.ReplacePlayerIndex(2);
            Assert.IsNull(game.GetEntityWithPlayerIndex(1));
            Assert.AreSame(e, game.GetEntityWithPlayerIndex(2));

            e.RemovePlayerIndex();
            Assert.IsNull(game.GetEntityWithPlayerIndex(2));
        }

        [Test]
        public void Primary_DuplicateKeyThrows()
        {
            _contexts.game.CreateEntity().AddPlayerIndex(1);
            Assert.Throws<EntityIndexException>(() => _contexts.game.CreateEntity().AddPlayerIndex(1));
        }

        [Test]
        public void Entity_FindAfterAddReplaceRemove()
        {
            var game = _contexts.game;
            var a = game.CreateEntity();
            var b = game.CreateEntity();
            a.AddTeam("red");
            b.AddTeam("red");
            Assert.AreEqual(2, game.GetEntitiesWithTeam("red").Count);

            b.ReplaceTeam("blue");
            Assert.AreEqual(1, game.GetEntitiesWithTeam("red").Count);
            Assert.IsTrue(game.GetEntitiesWithTeam("blue").Contains(b));

            a.RemoveTeam();
            Assert.AreEqual(0, game.GetEntitiesWithTeam("red").Count);
        }

        [Test]
        public void Index_SeesEntitiesAfterDestroy()
        {
            var e = _contexts.game.CreateEntity();
            e.AddPlayerIndex(5);
            e.Destroy();
            Assert.IsNull(_contexts.game.GetEntityWithPlayerIndex(5));
        }

        [Test]
        public void Indices_ArePerContextsInstance()
        {
            var other = new Contexts();
            _contexts.game.CreateEntity().AddPlayerIndex(1);
            Assert.IsNull(other.game.GetEntityWithPlayerIndex(1));
        }
    }
}