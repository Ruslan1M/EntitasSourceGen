# EntitasSourceGen

A Roslyn incremental source generator for **Entitas 1.14.1** that replaces Jenny.
Code is generated while Unity compiles. There is no external tool, no `Jenny.properties`, and no generated files in the repo.

- Unity 6 (tested on 6000.3), C# 9 output.
- The generated API matches Jenny 1.14 (`AddX` / `ReplaceX` / `RemoveX` / `hasX` / `isX`, matchers, unique, entity indices, events, cleanup), so existing game code keeps compiling.
- It generates code only in assemblies that opt in with a marker attribute.

## Why

| | Jenny 1.14 | Entitas 2.x | EntitasSourceGen |
|---|---|---|---|
| Runs on Unity 6 | `jenny server` exits, `gen` crashes ([#1055](https://github.com/sschmid/Entitas/issues/1055)) | never released, abandoned ([#1074](https://github.com/sschmid/Entitas/issues/1074)) | yes |
| Toolchain | external CLI, .NET 6 (EOL) | – | none, runs inside the compiler |
| Generated files | committed to the repo | – | in memory |
| Rename / broken compilation | reflection mode breaks | – | parses source, keeps working |
| Multi-assembly projects | one project | – | per-assembly gate |

## Install

The package is the `Package/` folder of this repository (UPM package `com.ruslan1m.entitas-sourcegen`). Add it to `Packages/manifest.json` in one of two ways.

**Local clone.** Edits in the clone show up in every project immediately:
```json
"com.ruslan1m.entitas-sourcegen": "file:../../EntitasSourceGen/Package"
```

**Git URL** (pin a tag):
```json
"com.ruslan1m.entitas-sourcegen": "https://github.com/Ruslan1M/EntitasSourceGen.git?path=/Package#v1.1.0"
```

To run the package tests in a project, add `"testables": ["com.ruslan1m.entitas-sourcegen"]` to its manifest.

Do not also keep `Entitas.dll` / Jenny DLLs in `Assets/`. The package already ships Entitas 1.14.1, and two copies of the same DLL cause conflicts.

## Package layout

```
Package/
  package.json
  Runtime/EntitasSourceGen.asmdef        attributes, no dependencies
  Runtime/Attributes/*.cs
  Runtime/Analyzers/EntitasSourceGen.Generator.dll   label RoslynAnalyzer, all platforms off (.meta is committed)
  Runtime/Plugins/Entitas/               Entitas 1.14.1 runtime + Visual Debugging (Editor/ = editor-only DLLs)
  Editor/                                Unity 6 style shim for the Visual Debugging inspector
  Tests/Editor/                          EditMode tests; the test assembly is itself a marker consumer
  Samples~/Basic/                        importable sample (Package Manager → Samples)
Generator/                               .NET solution: generator + tests (outside the package)
build.ps1                                builds Release and copies the DLL into Package/Runtime/Analyzers/
```

## Setup

### In a project
1. Install the package (see above).
2. In the asmdef that holds your components:
   - add a reference to `EntitasSourceGen`;
   - with **Override References** on, add these precompiled references: `Entitas.dll`, `DesperateDevs.Caching.dll`, `DesperateDevs.Reflection.dll`, `DesperateDevs.Extensions.dll`, `Sherlog.dll`.
3. Add the marker and the contexts:

```csharp
// AssemblyInfo.cs
[assembly: EntitasSourceGen.EntitasSourceGen(Namespace = "My.Game", Singleton = false)]

// ContextAttributes.cs — one attribute per context; the context name is the class name without "Attribute"
public sealed class GameAttribute : EntitasSourceGen.ContextAttribute { }
public sealed class InputAttribute : EntitasSourceGen.ContextAttribute { }
```

| Marker option | Default | |
|---|---|---|
| `Namespace` | global namespace (like Jenny) | namespace of every generated type |
| `Singleton` | `false` | generates `Contexts.sharedInstance` |

### Sample
Import **Basic** from Package Manager → *Entitas Source Generator* → *Samples*. It contains a marker, two contexts, components with unique, index, event and cleanup attributes, a reactive system and a `MonoBehaviour` composition root with Visual Debugging.

### Updating the generator
Run `./build.ps1` (add `-Test` to run the generator tests as well), then commit `Package/Runtime/Analyzers/EntitasSourceGen.Generator.dll`. The script replaces only the DLL. The `.meta` file with the `RoslynAnalyzer` label stays as it is. Bump `version` in `package.json` and add an entry to `CHANGELOG.md`.
## Usage

```csharp
using Entitas;
using EntitasSourceGen;

[Game] public sealed class LivesComponent : IComponent { public int Value; }
[Game] public sealed class AliveComponent : IComponent { }                      // flag → isAlive
[Game, Unique] public sealed class ScoreComponent : IComponent { public int Value; }
[Game] public sealed class PlayerIndexComponent : IComponent { [PrimaryEntityIndex] public int Value; }
[Game] public sealed class TeamComponent : IComponent { [EntityIndex] public string Value; }
[Game] public sealed class TargetIdComponent : IComponent { [EntityIndex("StatusesOfTarget")] public int Value; }   // composite key
[Game] public sealed class StatusTypeComponent : IComponent { [EntityIndex("StatusesOfTarget")] public int Value; }
[Game, Input] public sealed class IdComponent : IComponent { public int Value; }   // several contexts → IIdEntity<T>
[Game, Event(EventTarget.Self)] public sealed class PositionComponent : IComponent { public float X; public float Y; }
[Game, Event(EventTarget.Any, EventType.Removed)] public sealed class TargetComponent : IComponent { public int Id; }
[Input, Cleanup(CleanupMode.RemoveComponent)] public sealed class ClickComponent : IComponent { public int Button; }
[Input, Cleanup(CleanupMode.DestroyEntity)] public sealed class OneShotComponent : IComponent { }
[Game, FlagPrefix("can")] public sealed class MoveComponent : IComponent { }       // canMove
[Game, DontGenerate] public sealed class RawComponent : IComponent { public int Value; }  // index + matcher only
```

```csharp
var contexts = new Contexts();                       // no singleton unless enabled
var e = contexts.game.CreateEntity();
e.AddLives(3).ReplaceLives(2);                        // fluent: Add / Replace / Remove return the entity
int lives = e.Lives;                                 // Value shortcut for e.lives.Value
e.RemoveLives();
e.isAlive = true;
contexts.game.SetScore(10);                          // unique: SetX / ReplaceX / RemoveX / xEntity / hasX
var p = contexts.game.GetEntityWithPlayerIndex(1);   // primary index
var red = contexts.game.GetEntitiesWithTeam("red");  // entity index
var poisons = contexts.game.GetEntitiesWithStatusesOfTarget(targetId: 1, statusType: 2);   // composite index
e.AddPositionListener(view);                          // view : IPositionListener { OnPosition(GameEntity, float x, float y) }

var systems = new Feature()
    .Add(new GameEventSystems(contexts))
    .Add(new InputCleanupSystems(contexts));
systems.Execute(); systems.Cleanup();
```

### What is generated (per context, `Game` as example)

| File | Content |
|---|---|
| `Game.Lookup.g.cs` | `GameComponentsLookup`: indices sorted by name (ordinal), `TotalComponents`, `componentNames`, `componentTypes` |
| `Game.Context.g.cs` | `GameContext : Entitas.Context<GameEntity>` |
| `Game.Entity.g.cs` | `GameEntity`: `x`, `X` (single `Value`), `hasX`, `AddX`, `ReplaceX`, `RemoveX` (return the entity), `isX` |
| `Game.Matcher.g.cs` | `GameMatcher.X`, `AllOf`, `AnyOf` |
| `Game.Unique.g.cs` | unique API on `GameContext` |
| `Game.EntityIndices.g.cs` | `GetEntityWithX` / `GetEntitiesWithX` on `GameContext` |
| `Game.Events.g.cs` | `IXListener`, `XListenerComponent`, `XEventSystem`, `GameEventSystems : Feature` |
| `Game.Cleanup.g.cs` | `RemoveXGameSystem` / `DestroyXGameSystem`, `GameCleanupSystems : Feature` |
| `Contexts.g.cs`, `Contexts.EntityIndices.g.cs` | `Contexts`, index names, explicit index registration (composite keys are `ValueTuple`s) |
| `Components.Interfaces.g.cs` | `I{X}Entity<TEntity>` / `I{X}Entity` for components in several contexts |
| `Feature.g.cs` | `Feature`, as in Jenny: `DebugSystems` in the Editor (if the assembly references `Entitas.VisualDebugging.Unity`), profiled `Systems` in development builds, plain `Systems` otherwise. It is always generated unless you declare a `Feature` class in the same namespace yourself. Your own `Feature` must be a class derived from `Entitas.Systems` with a parameterless constructor; nothing checks this for you |

To view the generated code, open Rider → *Dependencies → Analyzers → EntitasSourceGen.Generator*, or Visual Studio → *Analyzers*.

### Rules
- The component name is the class name without the `Component` suffix. A class with no assignable members is a flag.
- A component can belong to several contexts. In that case its event names get a context prefix (`IGameTickListener`), but the listener methods do not (`OnTick(GameEntity …)` / `OnTick(InputEntity …)`).
- A component's members are its public instance fields and public get/set properties, including inherited ones. `readonly` fields and get-only / init-only properties are skipped.
- An index is named after the component when the component has a single member. Otherwise the name is the component name plus the member name (`CellX`).

## Diagnostics

| ID | Severity | When |
|---|---|---|
| ECG001 | Warning | `IComponent` without a context attribute (in a marker assembly) |
| ECG002 | Error | Two components map to the same name in one context; the second one is skipped |
| ECG003 | Error | Component class is generic, nested or non-public, or a member uses a non-public type |
| ECG004 | Error | `[Unique]` on a component that is in several contexts; the component is skipped |
| ECG005 | Warning | Component with a context attribute in an assembly **without** the marker |
| ECG006 | Error | A generated name clashes with an Entitas member, another generated member or an existing type; that part is skipped |
| ECG007 | Warning | Marker present but no context declared |
| ECG008 | Error | Both `[PrimaryEntityIndex]` and `[EntityIndex]` on one member |
| ECG009 | Warning | Index attribute on a member that is not generated (readonly, get-only, static, non-public) |
| ECG010 | Warning | Attribute ignored: invalid `[FlagPrefix]`, marker `Namespace` or index name (falls back to `is` / the global namespace / the default name), or `[Unique]` / `[Event]` / `[Cleanup]` / an index on a `[DontGenerate]` component |
| ECG011 | Error | A named index mixes `[PrimaryEntityIndex]` and `[EntityIndex]`; it is not generated |
| ECG012 | Warning | A named index is not generated in a context that lacks some of its components |

Generator exceptions show up in the Unity console as `CS8785`.

## Pipeline

```
[assembly: EntitasSourceGen]  → CompilationProvider.Select → GeneratorConfig? (equatable)
class X : IComponent          → SyntaxProvider (classes with base list) → ComponentCandidate (model + location)
                                   ├─ models only → Collect → EquatableArray ─┐
class GameAttribute : Context… → SyntaxProvider → context names ─────────────┤
types shaped like generated ones → SyntaxProvider → names ────────────────────┤
                                                                               ├─ Analysis (shared, pure)
                                   ├─ emit:        Combine(config) → AddSource ┘
                                   └─ diagnostics: candidates + locations → ReportDiagnostic
```

- The models contain only strings, bools, enums and `EquatableArray`. There are no `ISymbol`, `SyntaxNode` or `Location` objects in them, so incremental caching works. Moving a component re-runs diagnostics only. The tests check that emission stays cached.
- `Analysis` is shared by emission and diagnostics. Both agree on what is generated, and a conflicting component is skipped instead of producing code that does not compile.
- Listener components produced by events are added to the model in the same pipeline, because a generator never sees its own output.
- Gate: Unity passes the analyzer to `EntitasSourceGen` and to **every assembly that references it**, including transitive ones. This was verified in the compiler response files on Unity 6000.3: assemblies that reference only the consumer assembly also get the analyzer. Code is generated only where the marker is. Without the gate you would get duplicate types (CS0436).

## Migrating from Jenny
1. Delete the Jenny folders, `Jenny.properties` and the `Generated/` folder.
2. Remove the old Entitas DLLs from `Assets/` and install this package instead. It contains `Entitas.CodeGeneration.Attributes.dll` only as an explicitly referenced Editor DLL, which Visual Debugging needs.
3. Replace `using Entitas.CodeGeneration.Attributes;` with `using EntitasSourceGen;`. The attribute names are the same.
4. Declare a context attribute for each context that used to be in `Jenny.properties` (`Entitas.CodeGeneration.Plugins.Contexts`).
5. Add the assembly marker. Leave `Namespace` unset to keep the global namespace.
6. Not supported: `[ComponentName]`, `[CustomComponentName]`, `[PostConstructor]`, blueprints, migration. Components must live in the marker assembly (no components from referenced DLLs).
7. Replace `Contexts.sharedInstance` with an explicit `Contexts` instance, or set `Singleton = true`.

## Visual Debugging on Unity 6
The Entitas 1.14 Visual Debugging DLLs are built for Unity 2021. Their entity inspector looks up GUI styles named `ToolbarSeach*`, which Unity 6 no longer has. `Editor/EntitasUnity6StyleShim.cs` (assembly `EntitasSourceGen.Editor`) adds those names back as aliases.

The shim is Editor-only. Attach the debugging in your composition root:

```csharp
#if UNITY_EDITOR
foreach (var ctx in contexts.allContexts) new Entitas.VisualDebugging.Unity.ContextObserver(ctx);
#endif
```

## Development
- `dotnet test Generator/EntitasSourceGen.sln` runs `CSharpGeneratorDriver` tests against the real `Entitas.dll`. They cover C# 9 compilation, the gate, caching, every diagnostic and Verify snapshots per emitter. To accept snapshot changes, replace `*.verified.cs` with `*.received.cs`.
- CI (`.github/workflows/ci.yml`) runs only the generator tests, because running Unity in CI needs a license.
- Requirements: the generator project targets netstandard2.0 and uses `Microsoft.CodeAnalysis.CSharp` **4.3.0**. Newer Roslyn packages do not load in Unity 6.

## License
MIT, see [LICENSE.md](LICENSE.md). Entitas 1.14.1 binaries: MIT, by Simon Schmid.
