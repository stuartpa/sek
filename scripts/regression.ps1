<#
.SYNOPSIS
    SEK sample-exploration regression runner (data-driven).

.DESCRIPTION
    Explores each (project, machine) entry in the manifest and compares the resulting
    state / transition / accepting counts against the recorded baseline. This is the
    regression gate that protects the engine against changes: every sample must keep
    exploring to its known-good shape.

    Adding a new Cord sample project is just adding entries to the manifest (with -1
    counts) and running with -Update once to record the baseline.

.PARAMETER Manifest
    Path to the JSON manifest (default: samples/regression.manifest.json).

.PARAMETER Dll
    Path to the built sek.dll (default: src/Sek.Cli/bin/Debug/sek.dll).

.PARAMETER Update
    Record mode: run each entry and write the observed counts back to the manifest
    instead of comparing. Use to seed a baseline for a newly added sample.

.PARAMETER TimeoutSec
    Per-machine timeout in seconds (default 180). Exceeding it is a failure.

.EXAMPLE
    pwsh scripts/regression.ps1                 # verify against the baseline
    pwsh scripts/regression.ps1 -Update         # record/refresh the baseline
#>
[CmdletBinding()]
param(
    [string]$Manifest = "$PSScriptRoot/../samples/regression.manifest.json",
    [string]$Dll = "$PSScriptRoot/../src/Sek.Cli/bin/Debug/sek.dll",
    [switch]$Update,
    [int]$TimeoutSec = 180
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path "$PSScriptRoot/.."
$Dll = (Resolve-Path $Dll).Path

if (-not (Test-Path $Manifest)) { throw "Manifest not found: $Manifest" }
$doc = Get-Content $Manifest -Raw | ConvertFrom-Json
$entries = $doc.entries

$fails = @()
$results = @()

foreach ($e in $entries) {
    $proj = Join-Path $root $e.project
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $out = "$([System.IO.Path]::GetTempFileName())"
    $err = "$([System.IO.Path]::GetTempFileName())"
    $p = Start-Process -FilePath 'dotnet' `
        -ArgumentList "`"$Dll`" explore $($e.machine) --project `"$proj`"" `
        -RedirectStandardOutput $out -RedirectStandardError $err -NoNewWindow -PassThru
    if (-not $p.WaitForExit($TimeoutSec * 1000)) {
        try { $p.Kill($true) } catch { }
        $fails += "$($e.project)/$($e.machine): TIMEOUT (> ${TimeoutSec}s)"
        continue
    }

    $stdout = Get-Content $out -Raw -ErrorAction SilentlyContinue
    $stderr = Get-Content $err -Raw -ErrorAction SilentlyContinue
    Remove-Item $out, $err -ErrorAction SilentlyContinue

    if ($p.ExitCode -ne 0) {
        $fails += "$($e.project)/$($e.machine): exit $($p.ExitCode) — $($stderr.Trim())"
        continue
    }

    $m = [regex]::Match($stdout, "(\d+) states,\s*(\d+) transitions,\s*(\d+) accepting")
    if (-not $m.Success) {
        $fails += "$($e.project)/$($e.machine): could not parse exploration output"
        continue
    }

    $st = [int]$m.Groups[1].Value
    $tr = [int]$m.Groups[2].Value
    $ac = [int]$m.Groups[3].Value
    $secs = [math]::Round($sw.Elapsed.TotalSeconds, 1)

    if ($Update) {
        $e.states = $st; $e.transitions = $tr; $e.accepting = $ac
        $results += ('{0,-46}: {1} states, {2} transitions, {3} accepting [{4}s]' -f "$($e.project)/$($e.machine)", $st, $tr, $ac, $secs)
    }
    else {
        $ok = ($st -eq $e.states) -and ($tr -eq $e.transitions) -and ($ac -eq $e.accepting)
        $flag = if ($ok) { 'OK ' } else { 'FAIL' }
        $results += ('[{0}] {1,-46}: {2}/{3}/{4} (expected {5}/{6}/{7}) [{8}s]' -f $flag, "$($e.project)/$($e.machine)", $st, $tr, $ac, $e.states, $e.transitions, $e.accepting, $secs)
        if (-not $ok) {
            $fails += "$($e.project)/$($e.machine): got $st/$tr/$ac, expected $($e.states)/$($e.transitions)/$($e.accepting)"
        }
    }
}

$results | ForEach-Object { Write-Host $_ }

if ($Update) {
    $doc | ConvertTo-Json -Depth 5 | Set-Content $Manifest
    Write-Host "`nRecorded $($entries.Count) baseline(s) to $Manifest"
    exit 0
}

Write-Host ""
if ($fails.Count -gt 0) {
    Write-Host "REGRESSION FAILURES ($($fails.Count)):" -ForegroundColor Red
    $fails | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}

Write-Host "All $($entries.Count) sample explorations match the baseline." -ForegroundColor Green
exit 0
