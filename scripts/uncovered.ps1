<#
.SYNOPSIS
  List the uncovered lines (and optionally the partially-covered branches) for a package, read from
  the NEWEST cobertura report already on disk — no test re-run.

.DESCRIPTION
  Use after scripts/coverage.ps1 to see exactly what to target:
      pwsh scripts/uncovered.ps1 -Package sek            # fully-uncovered lines per file
      pwsh scripts/uncovered.ps1 -Package Sek.Cord -Branch  # partially-taken branches per file
  Cheap (parses the last report); pair with coverage.ps1 which produces the report.

.PARAMETER Package
  The cobertura package name (assembly): sek, Sek.Cord, Sek.Core, Sek.Engine, Sek.Modeling,
  SpecExplorerKit.Components.Solving, etc.

.PARAMETER Branch
  Instead of fully-uncovered lines, list lines whose branch condition-coverage is below 100%.
#>
param(
    [Parameter(Mandatory)][string]$Package,
    [switch]$Branch
)

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

$f = Get-ChildItem tests/Sek.Tests/TestResults -Recurse -Filter coverage.cobertura.xml -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime | Select-Object -Last 1 -ExpandProperty FullName
if (-not $f) { throw "no coverage report on disk — run scripts/coverage.ps1 first" }

[xml]$cov = Get-Content $f
$p = $cov.coverage.packages.package | Where-Object { $_.name -eq $Package }
if (-not $p) { throw "package '$Package' not found in report" }

"$Package  L=$('{0:P1}' -f [double]$p.'line-rate')  B=$('{0:P1}' -f [double]$p.'branch-rate')"

foreach ($c in $p.classes.class) {
    $file = $c.filename -replace '.*[\\/]', ''
    if ($Branch) {
        $partial = @($c.lines.line |
            Where-Object { $_.branch -eq 'true' -and $_.'condition-coverage' -notmatch '^100' } |
            ForEach-Object { "$($_.number):$($_.'condition-coverage')" })
        if ($partial.Count) { "{0} ({1}): {2}" -f $file, $partial.Count, ($partial -join '  ') }
    }
    else {
        $un = @($c.lines.line | Where-Object { $_.hits -eq '0' } | ForEach-Object { [int]$_.number })
        if ($un.Count) { "{0} ({1}): {2}" -f $file, $un.Count, (($un | Sort-Object) -join ',') }
    }
}
