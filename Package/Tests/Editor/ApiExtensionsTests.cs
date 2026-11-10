using Entitas;
using NUnit.Framework;

namespace Sample.Tests
{
    public class ApiExtensionsTests
    {
        Contexts _contexts;

        [SetUp]
        public void SetUp() => _contexts = new Contexts();

        [TearDown]
        public void TearDown() => _contexts.Reset();

        [Test]
        public void Fluent_AddReplaceRemove_ReturnEntity()
        {
            var e = _contexts.game.CreateEntity()
                .AddSpeed(1f)
                .AddLives(3)
                .ReplaceSpeed(2f)
                .RemoveLives();

            Assert.AreEqual(2f, e.Speed);
            Assert.IsFalse(e.hasLives);
        }

        [Test]
        public void ValueShortcut_EntityAndUnique()
        {
            var e = _contexts.game.CreateEntity().AddLives(3);
            Assert.AreEqual(3, e.Lives);

            _contexts.game.SetHeroId(7);
            Assert.AreEqual(7, _contexts.game.HeroId);
        }

        static TEntity Mark<TEntity>(TEntity e, int owner) where TEntity : IOwnerIdEntity<TEntity>, IMarkedEntity
        {
            e.isMarked = true;
            return e.ReplaceOwnerId(owner);
        }

        [Test]
        public void SharedInterfaces_WorkForEveryContext()
        {
            var g = Mark(_contexts.game.CreateEntity(), 1);
            var i = Mark(_contexts.input.CreateEntity(), 2);
            Assert.AreEqual(1, g.OwnerId);
            Assert.IsTrue(i.isMarked);

            IOwnerIdEntity any = i;
            IEntity same = any.ReplaceOwnerId(5);
            Assert.AreSame(i, same);
            Assert.AreEqual(5, any.OwnerId);
        }

        [Test]
        public void CompositeIndex_AcrossComponents_TracksAddReplaceRemove()
        {
            var game = _contexts.game;
            var poison = game.CreateEntity().AddTargetId(1).AddStatusKind(10);
            var freeze = game.CreateEntity().AddTargetId(1).AddStatusKind(20);
            game.CreateEntity().AddTargetId(2).AddStatusKind(10);

            // Key members are ordered by component name: (statusKind, targetId); named arguments keep calls readable.
            CollectionAssert.AreEquivalent(new[] { poison }, game.GetEntitiesWithStatusesOfTarget(targetId: 1, statusKind: 10));

            freeze.ReplaceStatusKind(10);
            CollectionAssert.AreEquivalent(new[] { poison, freeze }, game.GetEntitiesWithStatusesOfTarget(targetId: 1, statusKind: 10));
            Assert.IsEmpty(game.GetEntitiesWithStatusesOfTarget(targetId: 1, statusKind: 20));

            poison.RemoveTargetId();
            CollectionAssert.AreEquivalent(new[] { freeze }, game.GetEntitiesWithStatusesOfTarget(targetId: 1, statusKind: 10));
        }

        [Test]
        public void CompositePrimaryIndex_OneComponent()
        {
            var game = _contexts.game;
            var e = game.CreateEntity().AddGridCell(1, 2);
            Assert.AreSame(e, game.GetEntityWithCell(1, 2));
            Assert.IsNull(game.GetEntityWithCell(2, 1));
            Assert.Throws<EntityIndexException>(() => game.CreateEntity().AddGridCell(1, 2));
        }

        [Test]
        public void Feature_IsDebugSystems_InEditor()
        {
            var feature = new Feature();
            try
            {
                Assert.IsInstanceOf<Entitas.VisualDebugging.Unity.DebugSystems>(feature);
                Assert.AreEqual("Feature", feature.name);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(feature.gameObject);
            }
        }
    }
}
