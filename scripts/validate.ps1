# Validates SEK by building the toolkit, building each sample model, and exploring
# a representative machine from each sample. Exits non-zero on any failure. Used by
# CI (release workflow) and locally. Self-contained: only touches this repo.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$sek  = Join-Path $root 'src/Sek.Cli/bin/Debug/sek.dll'

Write-Host '== Building SEK toolkit =='
dotnet build (Join-Path $root 'src/Sek.Cli/Sek.Cli.csproj') -v q

# On Linux, the OS may carry an older system libz3.so (e.g. pulled in by LLVM) that
# lacks entry points the Microsoft.Z3 managed wrapper needs, and the NuGet package has
# no linux-x64 native. Copy a matching libz3.so next to sek.dll so the correct,
# app-local library is loaded first. CI provides one via $env:SEK_Z3_LIB.
if ($IsLinux) {
    $nativeDir = Split-Path $sek
    $lib = $env:SEK_Z3_LIB
    if (-not ($lib -and (Test-Path $lib))) {
        $found = Get-ChildItem "$HOME/.nuget/packages/microsoft.z3" -Recurse -Filter 'libz3.so' -ErrorAction SilentlyContinue |
                 Where-Object { $_.FullName -match 'linux' } | Select-Object -First 1
        if ($found) { $lib = $found.FullName }
    }
    if ($lib -and (Test-Path $lib)) {
        Copy-Item $lib $nativeDir -Force
        Write-Host "Copied Z3 native: $lib -> $nativeDir"
    }
    else {
        Write-Warning 'No linux libz3.so found. Set $env:SEK_Z3_LIB to a matching Microsoft.Z3 native, or install Z3 4.12.x.'
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
