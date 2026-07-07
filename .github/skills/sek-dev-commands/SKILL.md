---
name: sek-dev-commands
description: >
  The terse, committed commands for developing SpecExplorerKit (SEK): measure code coverage, find
  uncovered lines/branches, run the sample-exploration regression gate, build, and run the suite.
  Use these instead of re-typing the long coverage/uncovered pipelines. Applies to the SEK repo
  (contains src/Sek.*, tests/Sek.Tests, samples/, scripts/).
---

# SEK development commands (token-efficient)

Run all of these from the SEK repo root. They wrap the long, frequently-repeated pipelines into
committed scripts under `scripts/` so you don't re-type ~300-character command lines.

## Coverage

```powershell
pwsh scripts/coverage.ps1                 # run the full suite under coverage; print OVERALL + per-module L=line% B=branch%
pwsh scripts/coverage.ps1 -Package sek    # ... and also list sek's fully-uncovered lines per file
```
`coverage.ps1` kills stray dotnet processes (they lock the coverage file), clears the previous
`tests/Sek.Tests/TestResults`, collects "XPlat Code Coverage", and parses the newest cobertura report.
Runtime ≈ 2–4 min (the in-process CLI integration tests build sample models + drive samples).

## Uncovered lines / partial branches (no re-run — reads the last report)

```powershell
pwsh scripts/uncovered.ps1 -Package sek            # fully-uncovered line numbers per file
pwsh scripts/uncovered.ps1 -Package Sek.Cord -Branch  # lines with <100% branch coverage (target for branch %)
```
Package names are the assemblies: `sek`, `Sek.Cord`, `Sek.Core`, `Sek.Engine`, `Sek.Modeling`,
`SpecExplorerKit.Components.{Json,Random,Graphs,Solving}`.

## Regression gate (sample explorations)

```powershell
pwsh scripts/regression.ps1               # verify all sample explorations match samples/regression.manifest.json
pwsh scripts/regression.ps1 -Update       # re-baseline after an intentional change
```
Requires sample models built first: `Get-ChildItem samples -Recurse -Filter *.Model.csproj | % { dotnet build $_.FullName -c Debug --nologo -v q }`

## Build / test

```powershell
dotnet build Sek.slnx -c Debug --nologo -v q                       # whole solution
dotnet test tests/Sek.Tests/Sek.Tests.csproj -c Debug --nologo -v q  # full suite (no coverage)
```

## Notes

- After `.cs` edits, if a build seems not to pick them up (EBUSY / stale), `taskkill /F /IM dotnet.exe /T`
  then rebuild (the coverage script already does the taskkill).
- The in-process CLI test harness is `tests/Sek.Tests/CliHost.cs` (invokes the `sek` entry via
  reflection). Sample models each have a **unique** AssemblyName so they co-load in one test process.
- Generated/coverage output (`**/TestResults/`, `**/.specexplorerkit/out/`, bin/obj) is gitignored —
  never commit it.
