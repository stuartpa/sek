# Ensures the Linux x64 Z3 native (libz3.so) matching Microsoft.Z3 is available at
# native/linux-x64/libz3.so. The Microsoft.Z3 NuGet package ships win-x64 and osx-x64
# natives but no linux-x64, so we fetch the matching official Z3 build once and cache it
# (the native/ folder is git-ignored). Sek.Cli bundles this into the tool so `sek` works
# on Linux out of the box.

param(
    [string]$Z3Version = '4.12.2',
    [string]$Glibc = '2.31'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dest = Join-Path $root 'native/linux-x64/libz3.so'

if (Test-Path $dest) {
    Write-Host "Z3 linux native already present: $dest"
    return
}

$url = "https://github.com/Z3Prover/z3/releases/download/z3-$Z3Version/z3-$Z3Version-x64-glibc-$Glibc.zip"
$tmpZip = Join-Path ([System.IO.Path]::GetTempPath()) "z3-$Z3Version-linux.zip"
$tmpDir = Join-Path ([System.IO.Path]::GetTempPath()) "z3-$Z3Version-linux"

Write-Host "Downloading $url"
Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $tmpZip -TimeoutSec 300

if (Test-Path $tmpDir) { Remove-Item -Recurse -Force $tmpDir }
Expand-Archive -Path $tmpZip -DestinationPath $tmpDir -Force

$so = Get-ChildItem $tmpDir -Recurse -Filter 'libz3.so' | Select-Object -First 1
if (-not $so) { throw "libz3.so not found in $url" }

New-Item -ItemType Directory -Force (Split-Path $dest) | Out-Null
Copy-Item $so.FullName $dest -Force
Write-Host "Cached Z3 linux native -> $dest ($([math]::Round((Get-Item $dest).Length/1MB,1)) MB)"
