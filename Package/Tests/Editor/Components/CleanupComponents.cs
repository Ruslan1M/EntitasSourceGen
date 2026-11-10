using Entitas;
using EntitasSourceGen;

namespace Sample
{
    [Input, Cleanup(CleanupMode.RemoveComponent)] public sealed class ClickComponent : IComponent { public int Button; }
    [Input, Cleanup(CleanupMode.RemoveComponent), FlagPrefix("was")] public sealed class PressedComponent : IComponent { }
    [Input, Cleanup(CleanupMode.DestroyEntity)] public sealed class OneShotComponent : IComponent { }
    [Input, DontGenerate] public sealed class RawComponent : IComponent { public int Value; }
}