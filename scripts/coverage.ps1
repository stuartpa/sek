<#
.SYNOPSIS
  Run the SEK unit + integration suite under code coverage and print overall + per-module
  line/branch percentages. Optionally list the uncovered lines for one package.

.DESCRIPTION
  Wraps the (long, repeated) coverage pipeline so callers can just run:
      pwsh scripts/coverage.ps1
      pwsh scripts/coverage.ps1 -Package sek        # also list sek's uncovered lines
  Kills stray dotnet processes first (they lock DLLs / the coverage file), clears the previous
  TestResults, collects "XPlat Code Coverage", then parses the newest cobertura report.

.PARAMETER Package
  If given, also prints the uncovered line numbers per file for that package (e.g. sek, Sek.Cord,
  Sek.Engine, Sek.Core, Sek.Modeling, SpecExplorerKit.Components.Solving).
#>
param([string]$Package)

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

taskkill /F /IM dotnet.exe /T 2>$null | Out-Null
Remove-Item tests/Sek.Tests/TestResults -Recurse -Force -ErrorAction SilentlyContinue

dotnet test tests/Sek.Tests/Sek.Tests.csproj -c Debug `
    --collect:"XPlat Code Coverage" --results-directory tests/Sek.Tests/TestResults `
    --nologo -v q 2>&1 | Select-String -Pattern 'Passed!|Failed!' | Select-Object -Last 1

$f = Get-ChildItem tests/Sek.Tests/TestResults -Recurse -Filter coverage.cobertura.xml |
    Sort-Object LastWriteTime | Select-Object -Last 1 -ExpandProperty FullName
if (-not $f) { throw "no coverage report produced" }

[xml]$cov = Get-Content $f
"OVERALL L={0:P1} B={1:P1}" -f [double]$cov.coverage.'line-rate', [double]$cov.coverage.'branch-rate'
$cov.coverage.packages.package | Sort-Object name | ForEach-Object {
    "{0,-40} L={1,6:P1} B={2,6:P1}" -f $_.name, [double]$_.'line-rate', [double]$_.'branch-rate'
}

if ($Package) {
    & (Join-Path $PSScriptRoot 'uncovered.ps1') -Package $Package
}
