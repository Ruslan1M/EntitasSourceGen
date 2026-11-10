<#
.SYNOPSIS
  Builds the EntitasSourceGen generator (Release) and drops the DLL into Package/Runtime/Analyzers.
.PARAMETER Test
  Also runs generator unit tests.
#>
param(
    [switch]$Test
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$project = Join-Path $root 'Generator/EntitasSourceGen.Generator/EntitasSourceGen.Generator.csproj'
$outDir = Join-Path $root 'Generator/EntitasSourceGen.Generator/bin/Release/netstandard2.0'
$analyzers = Join-Path $root 'Package/Runtime/Analyzers'

dotnet build $project -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Generator build failed' }

if ($Test) {
    $env:DiffEngine_Disabled = 'true'
    dotnet test (Join-Path $root 'Generator/EntitasSourceGen.Generator.Tests') -c Release --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'Generator tests failed' }
}

New-Item -ItemType Directory -Force $analyzers | Out-Null
# Copy only the DLL: the committed .meta (RoslynAnalyzer label, platforms off) stays untouched.
Copy-Item (Join-Path $outDir 'EntitasSourceGen.Generator.dll') $analyzers -Force
Write-Host "Generator -> $analyzers"