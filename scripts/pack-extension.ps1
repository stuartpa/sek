# Builds the SpecExplorerKit release assets into ./dist:
#   - spec-kit-sek.zip   : the Spec Kit extension (extension.yml at archive root)
#   - SpecExplorerKit.Tool.<version>.nupkg : the `sek` .NET global tool
# and prints the extension zip's SHA-256 (for the community catalog entry).

param(
    [string]$Version = '0.1.0',
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null

# 1) Extension archive: zip the *contents* of extensions/spec-kit-sek so that
#    extension.yml sits at the root of the archive (required by `specify extension add`).
$extDir = Join-Path $root 'extensions/spec-kit-sek'
$extZip = Join-Path $dist 'spec-kit-sek.zip'
if (Test-Path $extZip) { Remove-Item $extZip -Force }
Compress-Archive -Path (Join-Path $extDir '*') -DestinationPath $extZip
$sha = (Get-FileHash -Algorithm SHA256 $extZip).Hash.ToLower()
Write-Host "Extension archive : $extZip"
Write-Host "SHA-256           : $sha"

# 2) The `sek` .NET global tool package.
$cli = Join-Path $root 'src/Sek.Cli/Sek.Cli.csproj'
dotnet pack $cli -c $Configuration -o $dist /p:Version=$Version | Out-Null
Get-ChildItem $dist -Filter '*.nupkg' | ForEach-Object { Write-Host "Tool package      : $($_.FullName)" }

# 3) Stamp the sha256 into the catalog entry for convenience.
$catalog = Join-Path $root 'extensions/catalog.community.json'
if (Test-Path $catalog) {
    $json = Get-Content -Raw $catalog | ConvertFrom-Json
    $json.extensions[0].sha256 = $sha
    ($json | ConvertTo-Json -Depth 10) | Set-Content -Path $catalog -Encoding UTF8
    Write-Host "Stamped sha256 into $catalog"
}
