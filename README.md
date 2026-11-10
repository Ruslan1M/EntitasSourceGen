# EntitasSourceGen

A Roslyn incremental source generator for Entitas 1.14.1 on Unity 6, as a UPM package. It replaces Jenny.

- `Package/` contains the Unity package `com.ruslan1m.entitas-sourcegen`. Its docs are in [Package/README.md](Package/README.md).
- `Generator/` contains the .NET solution with the generator (netstandard2.0, Roslyn 4.3) and its tests.
- `build.ps1` builds the generator and copies the DLL into `Package/Runtime/Analyzers/`. Use `-Test` to run the tests as well.

```bash
dotnet test Generator/EntitasSourceGen.sln   # generator tests
```