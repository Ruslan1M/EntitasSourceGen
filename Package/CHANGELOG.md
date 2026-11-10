# Changelog

## [1.1.0] - 2026-11-10

### Added
- `Value` shortcut: a component with one member named `Value` gets `entity.X` (and `context.X` for `[Unique]`), e.g. `entity.Speed` for `entity.speed.Value`. It is left out when the name is a built-in Entity/Context member.
- `I{X}Entity<TEntity>` / `I{X}Entity` interfaces for components in several contexts, implemented by every entity of those contexts (`Components.Interfaces.g.cs`).
- Named and composite entity indices: `[EntityIndex("Name")]` / `[PrimaryEntityIndex("Name")]` on members of one or several components form one `ValueTuple` key, with `GetEntitiesWithName(a, b)` / `GetEntityWithName(a, b)`.
- Diagnostics ECG011 (named index mixes index kinds) and ECG012 (named index incomplete in a context).

### Changed
- Fluent entity API: `AddX` / `ReplaceX` / `RemoveX` return the entity.
- `Feature` (Jenny parity): `DebugSystems` in the Editor when the assembly references `Entitas.VisualDebugging.Unity`, named after its type ("Movement Feature"); a profiled `Systems` in development builds; a plain `Systems` otherwise.

## [1.0.1] - 2026-11-10

### Changed
- `Feature` is generated in every marker assembly that has contexts (as in Jenny), not only when events or cleanups exist. Declaring your own `Feature` in the same namespace still turns it off.

## [1.0.0] - 2026-11-10

### Added
- Distributed as a UPM package (`com.ruslan1m.entitas-sourcegen`), with EditMode tests in `Tests/Editor` and a `Basic` sample.
- Incremental generator for Entitas 1.14.1, gated by `[assembly: EntitasSourceGen(Namespace, Singleton)]`.
- Contexts declared by `ContextAttribute` subclasses.
- Generated types: components lookup, context, entity API, matcher, unique API and `Contexts` (no singleton by default).
- Entity indices (`[PrimaryEntityIndex]`, `[EntityIndex]`), registered explicitly in `Contexts` without reflection.
- Events (`[Event(EventTarget.Self/Any, EventType.Added/Removed, priority)]`) with listener components, event systems and `{Ctx}EventSystems`.
- Cleanup (`[Cleanup(RemoveComponent/DestroyEntity)]`) with `{Ctx}CleanupSystems`.
- `[FlagPrefix]` and `[DontGenerate(generateIndex)]`.
- A generated `Feature`, emitted only when the project has events or cleanups and does not declare its own `Feature`.
- Diagnostics ECG001–ECG010.
- A Unity 6 style shim for the Entitas Visual Debugging inspector.
- Generator tests (C# 9 compilation, gate, cache, diagnostics, snapshots), CI and an Editor-only Sample with EditMode tests.
