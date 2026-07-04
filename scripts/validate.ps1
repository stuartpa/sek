# Validates SEK by building the toolkit, building each sample model, and exploring
# a representative machine from each sample. Exits non-zero on any failure. Used by
# CI (release workflow) and locally. Self-contained: only touches this repo.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$sek  = Join-Path $root 'src/Sek.Cli/bin/Debug/sek.dll'

Write-Host '== Building SEK toolkit =='
dotnet build (Join-Path $root 'src/Sek.Cli/Sek.Cli.csproj') -v q

# sample dir -> @(machines...). Operators is behavior-mode (no model project).
$samples = [ordered]@{
    'Operators'          = @('Party', 'SyncParallel', 'InterleavedParallel', 'Permutation', 'RepetitionOfAnyAction', 'Negation')
    'ParameterGeneration'= @('Product', 'Pairwise', 'Constraint')
    'Account'            = @('AccountExploration')
    'PubSub'             = @('PubSubExploration')
    'atsvc'              = @('JobScheduler')
    'chat'               = @('ChatProtocol')
    'SMB2'               = @('Smb2Lifecycle')
    'Sailboat'           = @('Voyage')
}

$failures = @()
foreach ($name in $samples.Keys) {
    $proj = Join-Path $root "samples/$name"
    # Build the model project if the sample has one.
    $csproj = Get-ChildItem -Path (Join-Path $proj 'Model') -Filter '*.csproj' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($csproj) {
        Write-Host "== Building model: $name =="
        dotnet build $csproj.FullName -v q
    }
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
