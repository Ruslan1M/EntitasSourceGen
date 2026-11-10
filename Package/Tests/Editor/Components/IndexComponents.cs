using Entitas;
using EntitasSourceGen;

namespace Sample
{
    [Game] public sealed class PlayerIndexComponent : IComponent { [PrimaryEntityIndex] public int Value; }
    [Game] public sealed class TeamComponent : IComponent { [EntityIndex] public string Value; }
}