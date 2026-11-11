# Entitas Source Generator

[![CI](https://github.com/Ruslan1M/EntitasSourceGen/actions/workflows/ci.yml/badge.svg)](https://github.com/Ruslan1M/EntitasSourceGen/actions/workflows/ci.yml)
![Unity 6](https://img.shields.io/badge/Unity-6000.0%2B-222)
![Entitas 1.14.1](https://img.shields.io/badge/Entitas-1.14.1-4a90d9)
![License: MIT](https://img.shields.io/badge/license-MIT-3fb8af)

A Roslyn incremental source generator that replaces Jenny for **Entitas 1.14.1 on Unity 6**. The full Entitas API is generated in memory while Unity compiles, so there is no separate code-gen step, no `Jenny.properties` and no `Generated/` folder in the repository.

## Why

- Jenny is broken on Unity 6 ([#1055](https://github.com/sschmid/Entitas/issues/1055), [#1074](https://github.com/sschmid/Entitas/issues/1074)).
- The generator parses source code instead of using reflection, so it keeps working when the project doesn't compile or a type was just renamed.
- Generated code shows up in the IDE (Rider / Visual Studio, *Analyzers* node) as you type.
- The output matches Jenny 1.14, so existing game code compiles without changes.

## Install

Add the package to `Packages/manifest.json`:

```json
"com.ruslan1m.entitas-sourcegen": "https://github.com/Ruslan1M/EntitasSourceGen.git?path=/Package#v1.1.0"
```

The package ships the Entitas 1.14.1 runtime and Visual Debugging DLLs. Setup, the full attribute reference and migration from Jenny are in **[Package/README.md](Package/README.md)**.

## What you write and what you get

```csharp
[Game, Unique, Event(EventTarget.Self)]
public sealed class LivesComponent : IComponent
{
    public int Value;
}
```

```csharp
// generated at compile time
entity.AddLives(3);
entity.ReplaceLives(entity.Lives - 1);   // Value shortcut
if (entity.hasLives) { ... }
contexts.game.livesEntity;               // [Unique]
entity.AddLivesListener(listener);       // [Event]
GameMatcher.Lives;
```

## Features

- **Per context:** `ComponentsLookup`, `Context`, entity API (`x`, `hasX`, fluent `AddX` / `ReplaceX` / `RemoveX`, `isX` flags, `Value` shortcut), `Matcher`, unique API, `GetEntityWithX` / `GetEntitiesWithX`, events and listener systems, cleanup systems.
- **Per assembly:** `Contexts` (optional singleton, explicit index registration with no reflection), `I{X}Entity<T>` interfaces for components shared by several contexts, `Feature` with Jenny parity (`DebugSystems` in the Editor, profiled `Systems` in development builds).
- **Attributes:** `[Unique]`, `[PrimaryEntityIndex]`, `[EntityIndex]` (including composite keys), `[Event]`, `[Cleanup]`, `[FlagPrefix]`, `[DontGenerate]`.
- **12 compile-time diagnostics** (`ECG001`–`ECG012`): missing context, name clashes, generic, nested or non-public components, invalid index combinations and more.
- **Visual Debugging fix for Unity 6:** an editor shim restores the GUI styles the Entitas inspector expects.

## Engineering

- `IIncrementalGenerator` with equatable models, separate emit and diagnostics pipelines, and tracked steps. Cache behaviour is covered by tests: an unchanged run, an unrelated edit and a moved component all stay cached.
- ~100 generator tests (NUnit + `CSharpGeneratorDriver`, [Verify](https://github.com/VerifyTests/Verify) snapshots). Generated code is compiled against the real `Entitas.dll`.
- ~35 Unity EditMode tests exercise the generated API inside Unity.
- GitHub Actions runs the generator tests on every push and pull request.

```bash
dotnet test Generator/EntitasSourceGen.sln   # generator tests
./build.ps1 -Test                            # build the DLL into Package/Runtime/Analyzers and run tests
```

## Repository layout

| Path | What it is |
|---|---|
| `Package/` | UPM package `com.ruslan1m.entitas-sourcegen`, docs, sample, EditMode tests |
| `Generator/` | .NET solution: the generator (netstandard2.0, Roslyn 4.3) and its tests |
| `build.ps1` | Builds the generator and copies the DLL into the package |

## License

MIT. The bundled Entitas 1.14.1 binaries are © Simon Schmid, MIT; see [NOTICE](NOTICE).
