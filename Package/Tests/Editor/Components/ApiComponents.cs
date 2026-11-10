using Entitas;
using EntitasSourceGen;

namespace Sample
{
    // 1.1: Value shortcut (entity.Speed), no "Component" suffix.
    [Game] public sealed class Speed : IComponent { public float Value; }
    [Game, Unique] public sealed class HeroIdComponent : IComponent { public int Value; }

    // Shared by two contexts: IOwnerIdEntity<TEntity> / IMarkedEntity.
    [Game, Input] public sealed class OwnerIdComponent : IComponent { public int Value; }
    [Game, Input] public sealed class MarkedComponent : IComponent { }

    // Composite indices: across two components, and two members of one component.
    [Game] public sealed class TargetIdComponent : IComponent { [EntityIndex("StatusesOfTarget")] public int Value; }
    [Game] public sealed class StatusKindComponent : IComponent { [EntityIndex("StatusesOfTarget")] public int Value; }
    [Game] public sealed class GridCellComponent : IComponent { [PrimaryEntityIndex("Cell")] public int X; [PrimaryEntityIndex("Cell")] public int Y; }
}
