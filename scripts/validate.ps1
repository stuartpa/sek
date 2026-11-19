# Validates SEK by building the complete modern solution and exploring
# a representative machine from each sample. Exits non-zero on any failure. Used by
# CI (release workflow) and locally. Self-contained: only touches this repo.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$sek  = Join-Path $root 'src/Sek.Cli/bin/Debug/net10.0/sek.dll'

# Microsoft.Z3 has no linux-x64 native. Cache the matching libz3.so so the build copies
# it next to sek.dll (the .NET loader then uses this app-local native on Linux instead
# of the runner's incompatible system libz3). No-op on Windows/macOS.
if ($IsLinux) {
    & (Join-Path $PSScriptRoot 'fetch-z3-linux.ps1')
}

Write-Host '== Building SEK solution =='
dotnet build (Join-Path $root 'Sek.slnx') -v q
if ($LASTEXITCODE -ne 0) { throw 'SEK solution build failed.' }

$manifest = Get-Content (Join-Path $root 'samples/regression.manifest.json') -Raw | ConvertFrom-Json

function Test-ExploreOutcome($entry, $exitCode, $output) {
    if ($entry -and $entry.expectedOutcome -eq 'INCOMPLETE') {
        return $exitCode -eq 1 -and $output.Contains("INCOMPLETE exploration: $($entry.terminationReason);")
    }
    return $exitCode -eq 0
}

# sample dir -> @(machines...). Operators is behavior-mode (no model project).
$samples = [ordered]@{
    'Operators'          = @('Party', 'SyncParallel', 'InterleavedParallel', 'Permutation', 'RepetitionOfAnyAction', 'Negation')
    'ParameterGeneration'= @('Product', 'Pairwise', 'Constraint')
    'Account'            = @('SlicedModelProgram')
    'PubSub'             = @('TwoSubscribersWithParametersSlice')
    'atsvc'              = @('ModelProgramWithTwoJobsPattern')
    'chat'               = @('CombinedSlices')
    'SMB2'               = @('AllSync', 'CheckAsyncCreateCloseForNoAsync')
    'Sailboat'           = @('PointAndShoot')
    'Turnstile'          = @('ModelProgram')
    'SelfHost'           = @('ModelProgram')
}

$failures = @()
foreach ($name in $samples.Keys) {
    $proj = Join-Path $root "samples/$name"
    foreach ($machine in $samples[$name]) {
        Write-Host "== Explore: $name / $machine =="
        $output = & dotnet $sek explore $machine --project $proj 2>&1
        $exitCode = $LASTEXITCODE
        $output | ForEach-Object { Write-Host $_ }
        $entry = $manifest.entries | Where-Object { $_.project -eq "samples/$name" -and $_.machine -eq $machine }
        if (-not (Test-ExploreOutcome $entry $exitCode ($output -join "`n"))) { $failures += "$name/$machine" }
    }
}

if ($failures.Count -gt 0) {
    Write-Error ("Validation FAILED for: " + ($failures -join ', '))
    exit 1
}
Write-Host "`nValidation PASSED: all samples matched their expected outcomes."
