using Entitas;
using EntitasSourceGen;

namespace Sample
{
    [Game] public sealed class PositionComponent : IComponent { public float X; public float Y; }
    [Game] public sealed class AliveComponent : IComponent { }
    [Game, Unique] public sealed class ScoreComponent : IComponent { public int Value; }
    [Game] public sealed class PlayerIdComponent : IComponent { [PrimaryEntityIndex] public int Value; }
    [Game, Event(EventTarget.Any)] public sealed class HealthComponent : IComponent { public int Value; }
    [Input, Cleanup(CleanupMode.DestroyEntity)] public sealed class MoveInputComponent : IComponent { public int PlayerId; public float Dx; public float Dy; }
}