using System.Collections.Generic;
using Entitas;
using NUnit.Framework;

namespace Sample.Tests
{
    /// <summary>Runtime behaviour of the generated M0 API (no generator internals).</summary>
    public class GeneratedApiTests
    {
        Contexts _contexts;

        [SetUp]
        public void SetUp() => _contexts = new Contexts();

        [TearDown]
        public void TearDown() => _contexts.Reset();

        [Test]
        public void Lookup_IsSortedByName_AndMatchesTypes()
        {
            CollectionAssert.AreEqual(
                new[] { "Alive", "Lives", "Name", "Object", "Paused", "PlayerIndex", "Position", "Score", "Team", "Tick" },
                GameComponentsLookup.componentNames);
            Assert.AreEqual(GameComponentsLookup.TotalComponents, GameComponentsLookup.componentTypes.Length);
            Assert.AreEqual(typeof(LivesComponent), GameComponentsLookup.componentTypes[GameComponentsLookup.Lives]);
        }

        [Test]
        public void Context_HasInfo()
        {
            Assert.AreEqual("Game", _contexts.game.contextInfo.name);
            Assert.AreEqual(GameComponentsLookup.TotalComponents, _contexts.game.totalComponents);
            Assert.AreEqual(UiComponentsLookup.TotalComponents, _contexts.ui.totalComponents);
            Assert.AreEqual(3, _contexts.allContexts.Length);
        }

        [Test]
        public void Entity_AddReplaceRemove()
        {
            var e = _contexts.game.CreateEntity();
            Assert.IsFalse(e.hasLives);

            e.AddLives(3);
            Assert.IsTrue(e.hasLives);
            Assert.AreEqual(3, e.lives.Value);

            e.ReplaceLives(2);
            Assert.AreEqual(2, e.lives.Value);

            e.RemoveLives();
            Assert.IsFalse(e.hasLives);
        }

        [Test]
        public void Entity_MultiFieldComponent()
        {
            var e = _contexts.game.CreateEntity();
            e.AddPosition(1f, 2f);
            Assert.AreEqual(1f, e.position.X);
            Assert.AreEqual(2f, e.position.Y);
        }

        [Test]
        public void Entity_KeywordNamedComponent()
        {
            var e = _contexts.game.CreateEntity();
            e.AddObject(new List<int> { 1 });
            Assert.IsTrue(e.hasObject);
            Assert.AreEqual(1, e.@object.Items[0]);
        }

        [Test]
        public void Entity_Flag()
        {
            var e = _contexts.game.CreateEntity();
            Assert.IsFalse(e.isAlive);
            e.isAlive = true;
            Assert.IsTrue(e.isAlive);
            e.isAlive = true; // idempotent
            e.isAlive = false;
            Assert.IsFalse(e.isAlive);
        }

        [Test]
        public void Entity_DataComponentsArePooled()
        {
            var e = _contexts.game.CreateEntity();
            e.AddLives(1);
            var first = e.lives;
            e.ReplaceLives(2);
            e.ReplaceLives(3);
            // Replace returns the old instance to the pool; the next Replace reuses it.
            Assert.AreSame(first, e.lives);
        }

        [Test]
        public void MultiContext_ComponentExistsInBoth()
        {
            _contexts.game.CreateEntity().AddTick(5);
            _contexts.input.CreateEntity().AddTick(7);
            Assert.AreEqual(5, _contexts.game.GetGroup(GameMatcher.Tick).GetSingleEntity().tick.Value);
            Assert.AreEqual(7, _contexts.input.GetGroup(InputMatcher.Tick).GetSingleEntity().tick.Value);
        }

        [Test]
        public void Unique_SetReplaceRemove()
        {
            var game = _contexts.game;
            Assert.IsFalse(game.hasScore);

            var e = game.SetScore(10);
            Assert.AreSame(e, game.scoreEntity);
            Assert.AreEqual(10, game.score.Value);

            game.ReplaceScore(20);
            Assert.AreEqual(20, game.score.Value);
            Assert.AreSame(e, game.scoreEntity);

            game.RemoveScore();
            Assert.IsFalse(game.hasScore);

            game.ReplaceScore(1); // Replace without Set creates the entity
            Assert.IsTrue(game.hasScore);
        }

        [Test]
        public void Unique_SecondSetThrows()
        {
            _contexts.game.SetScore(1);
            Assert.Throws<EntitasException>(() => _contexts.game.SetScore(2));
        }

        [Test]
        public void Unique_Flag()
        {
            var game = _contexts.game;
            Assert.IsFalse(game.isPaused);
            game.isPaused = true;
            Assert.IsTrue(game.isPaused);
            Assert.IsNotNull(game.pausedEntity);
            game.isPaused = false;
            Assert.IsFalse(game.isPaused);
            Assert.AreEqual(0, game.count);
        }

        [Test]
        public void Matcher_GroupReceivesEntity()
        {
            var group = _contexts.game.GetGroup(GameMatcher.AllOf(GameMatcher.Lives, GameMatcher.Alive));
            var e = _contexts.game.CreateEntity();
            e.AddLives(1);
            Assert.AreEqual(0, group.count);
            e.isAlive = true;
            Assert.AreEqual(1, group.count);
            Assert.AreSame(GameMatcher.Lives, GameMatcher.Lives);
            StringAssert.Contains("Lives", GameMatcher.Lives.ToString());
        }

        [Test]
        public void Matcher_AnyOf()
        {
            var group = _contexts.input.GetGroup(InputMatcher.AnyOf(InputMatcher.Hold, InputMatcher.UseItem));
            _contexts.input.CreateEntity().isHold = true;
            _contexts.input.CreateEntity().AddUseItem(0, 1);
            Assert.AreEqual(2, group.count);
        }

        [Test]
        public void ReactiveSystem_FiresOnReplace()
        {
            var system = new LivesChangedSystem(_contexts);
            var e = _contexts.game.CreateEntity();
            e.AddLives(3);
            system.Execute();
            system.Count = 0;

            e.ReplaceLives(2);
            system.Execute();
            Assert.AreEqual(1, system.Count);
        }

        [Test]
        public void Contexts_AreIndependentInstances()
        {
            var other = new Contexts();
            _contexts.game.CreateEntity();
            Assert.AreEqual(1, _contexts.game.count);
            Assert.AreEqual(0, other.game.count);
            Assert.AreNotSame(_contexts.game, other.game);
        }

        [Test]
        public void Singleton_IsOptIn()
        {
            // Sample enables Singleton = true; Shared does not (no sharedInstance there).
            Assert.IsNotNull(Contexts.sharedInstance);
            Assert.AreSame(Contexts.sharedInstance, Contexts.sharedInstance);
        }

        sealed class LivesChangedSystem : ReactiveSystem<GameEntity>
        {
            public int Count;

            public LivesChangedSystem(Contexts contexts) : base(contexts.game) { }

            protected override ICollector<GameEntity> GetTrigger(IContext<GameEntity> context)
                => context.CreateCollector(GameMatcher.Lives);

            protected override bool Filter(GameEntity entity) => entity.hasLives;

            protected override void Execute(List<GameEntity> entities) => Count += entities.Count;
        }
    }
}
