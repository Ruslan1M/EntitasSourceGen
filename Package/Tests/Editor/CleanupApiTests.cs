using NUnit.Framework;

namespace Sample.Tests
{
    public class CleanupApiTests
    {
        Contexts _contexts;
        InputCleanupSystems _cleanup;

        [SetUp]
        public void SetUp()
        {
            _contexts = new Contexts();
            _cleanup = new InputCleanupSystems(_contexts);
        }

        [TearDown]
        public void TearDown() => _contexts.Reset();

        [Test]
        public void RemoveComponent_DataAndFlag()
        {
            var e = _contexts.input.CreateEntity();
            e.AddClick(1);
            e.wasPressed = true;

            _cleanup.Cleanup();

            Assert.IsFalse(e.hasClick);
            Assert.IsFalse(e.wasPressed);
            Assert.IsTrue(e.isEnabled);
        }

        [Test]
        public void DestroyEntity()
        {
            _contexts.input.CreateEntity().isOneShot = true;
            var survivor = _contexts.input.CreateEntity();
            survivor.isHold = true;

            _cleanup.Cleanup();

            Assert.AreEqual(1, _contexts.input.count);
            Assert.IsTrue(survivor.isEnabled);
        }

        [Test]
        public void DontGenerate_HasIndexAndMatcher()
        {
            var e = _contexts.input.CreateEntity();
            e.AddComponent(InputComponentsLookup.Raw, new RawComponent { Value = 3 });
            Assert.AreEqual(1, _contexts.input.GetGroup(InputMatcher.Raw).count);
        }
    }
}