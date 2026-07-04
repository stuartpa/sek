# Validates SEK by building the toolkit, building each sample model, and exploring
# a representative machine from each sample. Exits non-zero on any failure. Used by
# CI (release workflow) and locally. Self-contained: only touches this repo.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$sek  = Join-Path $root 'src/Sek.Cli/bin/Debug/sek.dll'

Write-Host '== Building SEK toolkit =='
dotnet build (Join-Path $root 'src/Sek.Cli/Sek.Cli.csproj') -v q

# On Linux, the OS may carry an older system libz3.so (e.g. pulled in by LLVM) that
# lacks entry points the Microsoft.Z3 managed wrapper needs. Copy the NuGet-provided
# linux-x64 native next to sek.dll so the correct, app-local library is loaded first.
if ($IsLinux) {
    $nativeDir = Split-Path $sek
    $z3 = Get-ChildItem "$HOME/.nuget/packages/microsoft.z3" -Recurse -Filter 'libz3.so' -ErrorAction SilentlyContinue |
          Where-Object { $_.FullName -match 'linux-x64' } | Select-Object -First 1
    if ($z3) {
        Copy-Item $z3.FullName $nativeDir -Force
        Write-Host "Copied Z3 native: $($z3.FullName) -> $nativeDir"
    }
    else {
        Write-Warning 'Could not locate the Microsoft.Z3 linux-x64 libz3.so in the NuGet cache.'
    }
}

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
