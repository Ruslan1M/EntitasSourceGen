using Entitas;

namespace Sample
{
    [Input] public sealed class HoldComponent : IComponent { }
    [Input] public sealed class UseItemComponent : IComponent { public int Player; public int Slot; }
}