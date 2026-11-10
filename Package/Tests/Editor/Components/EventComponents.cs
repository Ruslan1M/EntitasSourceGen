using Entitas;
using EntitasSourceGen;

namespace Sample
{
    [Ui, Event(EventTarget.Self)] public sealed class HealthComponent : IComponent { public int Value; }
    [Ui, Event(EventTarget.Any), Event(EventTarget.Any, EventType.Removed)] public sealed class BannerComponent : IComponent { public string Text; }
    [Ui, Event(EventTarget.Self, EventType.Removed)] public sealed class VisibleComponent : IComponent { }
}