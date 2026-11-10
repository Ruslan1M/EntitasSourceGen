using Entitas;
using EntitasSourceGen;

namespace Sample
{
    [Game] public sealed class PositionComponent : IComponent { public float X; public float Y; }
    [Game] public sealed class LivesComponent : IComponent { public int Value; }
    [Game] public sealed class AliveComponent : IComponent { }
    [Game] public sealed class NameComponent : IComponent { public string Value; }
    [Game, Unique] public sealed class ScoreComponent : IComponent { public int Value; }
    [Game, Unique] public sealed class PausedComponent : IComponent { }
    [Game, Input] public sealed class TickComponent : IComponent { public long Value; }

    // Keyword-named component: generated members must be escaped (@object).
    [Game] public sealed class ObjectComponent : IComponent { public System.Collections.Generic.List<int> Items; }
}