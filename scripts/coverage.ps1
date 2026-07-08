<#
.SYNOPSIS
  Run the SEK unit + integration suite under code coverage and print overall + per-module
  line/branch percentages, optionally with a PASS/FAIL gate verdict.

.DESCRIPTION
  Wraps the (long, repeated) coverage pipeline so callers can just run:
      pwsh scripts/coverage.ps1                      # full suite, percentages
      pwsh scripts/coverage.ps1 -Package sek         # also list sek's uncovered lines
      pwsh scripts/coverage.ps1 -Stable -Bar 90      # stable+fast, PASS/FAIL per module vs 90%
  Kills stray dotnet processes first (they lock DLLs / the coverage file), clears the previous
  TestResults, collects "XPlat Code Coverage", then parses the newest cobertura report.

.PARAMETER Package
  If given, also prints the uncovered line numbers per file for that package (e.g. sek, Sek.Cord,
  Sek.Engine, Sek.Core, Sek.Modeling, SpecExplorerKit.Components.Solving).

.PARAMETER Stable
  Exclude the flaky, slow subprocess self-model test (Test_SelfHost_Conformance) from the coverage
  run. Its coverage is redundant (subprocess coverage isn't captured) and it makes the numbers
  non-deterministic (+-0.5%). Use this for a fast, reproducible measurement.

.PARAMETER Bar
  If given (e.g. 90 or 95), append a PASS/FAIL column per module: PASS iff line% AND branch% are
  >= the bar. Also sets the process exit code to 1 if any module FAILs (0 otherwise).
#>
param(
    [string]$Package,
    [switch]$Stable,
    [double]$Bar = 0
)

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

taskkill /F /IM dotnet.exe /T 2>$null | Out-Null
Remove-Item tests/Sek.Tests/TestResults -Recurse -Force -ErrorAction SilentlyContinue

$testArgs = @(
    'test', 'tests/Sek.Tests/Sek.Tests.csproj', '-c', 'Debug',
    '--collect:XPlat Code Coverage', '--results-directory', 'tests/Sek.Tests/TestResults',
    '--nologo', '-v', 'q'
)
if ($Stable) { $testArgs += @('--filter', 'FullyQualifiedName!~Test_SelfHost_Conformance') }

dotnet @testArgs 2>&1 | Select-String -Pattern 'Passed!|Failed!' | Select-Object -Last 1

$f = Get-ChildItem tests/Sek.Tests/TestResults -Recurse -Filter coverage.cobertura.xml |
    Sort-Object LastWriteTime | Select-Object -Last 1 -ExpandProperty FullName
if (-not $f) { throw "no coverage report produced" }

[xml]$cov = Get-Content $f
$bar = $Bar / 100.0
$anyFail = $false
"OVERALL L={0:P1} B={1:P1}" -f [double]$cov.coverage.'line-rate', [double]$cov.coverage.'branch-rate'
$cov.coverage.packages.package | Sort-Object name | ForEach-Object {
    $l = [double]$_.'line-rate'; $b = [double]$_.'branch-rate'
    if ($Bar -gt 0) {
        $ok = ($l -ge $bar) -and ($b -ge $bar)
        if (-not $ok) { $script:anyFail = $true }
        "{0,-40} L={1,6:P1} B={2,6:P1} {3}" -f $_.name, $l, $b, ($(if ($ok) { 'PASS' } else { 'FAIL' }))
    }
    else {
        "{0,-40} L={1,6:P1} B={2,6:P1}" -f $_.name, $l, $b
    }
}

if ($Package) {
    & (Join-Path $PSScriptRoot 'uncovered.ps1') -Package $Package
}

if ($Bar -gt 0 -and $anyFail) { exit 1 }
