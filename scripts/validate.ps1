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
        & dotnet $sek explore $machine --project $proj
        if ($LASTEXITCODE -ne 0) { $failures += "$name/$machine" }
    }
}

if ($failures.Count -gt 0) {
    Write-Error ("Validation FAILED for: " + ($failures -join ', '))
    exit 1
}
Write-Host "`nValidation PASSED: all samples explored successfully."
