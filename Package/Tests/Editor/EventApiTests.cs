using System.Collections.Generic;
using NUnit.Framework;

namespace Sample.Tests
{
    public class EventApiTests
    {
        sealed class Recorder : IHealthListener, IAnyBannerListener, IAnyBannerRemovedListener, IVisibleRemovedListener
        {
            public readonly List<string> Calls = new List<string>();
            public void OnHealth(UiEntity entity, int value) => Calls.Add("health:" + value);
            public void OnAnyBanner(UiEntity entity, string text) => Calls.Add("banner:" + text);
            public void OnAnyBannerRemoved(UiEntity entity) => Calls.Add("banner-removed");
            public void OnVisibleRemoved(UiEntity entity) => Calls.Add("visible-removed");
        }

        Contexts _contexts;
        UiEventSystems _events;
        Recorder _recorder;

        [SetUp]
        public void SetUp()
        {
            _contexts = new Contexts();
            _events = new UiEventSystems(_contexts);
            _recorder = new Recorder();
        }

        [TearDown]
        public void TearDown()
        {
            _events.DeactivateReactiveSystems();
            _contexts.Reset();
        }

        [Test]
        public void Self_Added_NotifiesOnlyOwnListener()
        {
            var watched = _contexts.ui.CreateEntity();
            watched.AddHealthListener(_recorder);
            var other = _contexts.ui.CreateEntity();

            watched.AddHealth(10);
            other.AddHealth(99);
            _events.Execute();
            CollectionAssert.AreEqual(new[] { "health:10" }, _recorder.Calls);

            watched.ReplaceHealth(5);
            _events.Execute();
            CollectionAssert.AreEqual(new[] { "health:10", "health:5" }, _recorder.Calls);
        }

        [Test]
        public void Self_RemoveListener_StopsNotifications()
        {
            var e = _contexts.ui.CreateEntity();
            e.AddHealthListener(_recorder);
            e.RemoveHealthListener(_recorder);
            Assert.IsFalse(e.hasHealthListener);

            e.AddHealth(1);
            _events.Execute();
            Assert.IsEmpty(_recorder.Calls);
        }

        [Test]
        public void Any_AddedAndRemoved()
        {
            _contexts.ui.CreateEntity().AddAnyBannerListener(_recorder);
            _contexts.ui.CreateEntity().AddAnyBannerRemovedListener(_recorder);

            var banner = _contexts.ui.CreateEntity();
            banner.AddBanner("hi");
            _events.Execute();
            banner.RemoveBanner();
            _events.Execute();

            CollectionAssert.AreEqual(new[] { "banner:hi", "banner-removed" }, _recorder.Calls);
        }

        [Test]
        public void Self_FlagRemoved()
        {
            var e = _contexts.ui.CreateEntity();
            e.AddVisibleRemovedListener(_recorder);
            e.isVisible = true;
            _events.Execute();
            Assert.IsEmpty(_recorder.Calls);

            e.isVisible = false;
            _events.Execute();
            CollectionAssert.AreEqual(new[] { "visible-removed" }, _recorder.Calls);
        }

        [Test]
        public void EventSystems_IsFeature()
        {
            Assert.IsInstanceOf<Feature>(_events);
            Assert.IsInstanceOf<Entitas.Systems>(_events);
        }
    }
}