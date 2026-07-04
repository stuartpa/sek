# RequirementReport (SEK port)
#
# The classic Spec Explorer RequirementReport sample is NOT a model to explore: it is a
# post-processing tool (PostProcessorSample) that reads explored transition systems and
# emits a coverage report. Its faithful SpecExplorerKit equivalent is the `sek view`
# post-processor, which turns any explored `.seexpl` graph into an HTML / DOT / Mermaid
# report. This script regenerates HTML reports for the explored sample graphs.

$sek = 'C:\boards\brd009\SEK\src\Sek.Cli\bin\Debug\sek.dll'
$outDir = 'C:\boards\brd009\SEK\samples\RequirementReport\reports'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$graphs = @{
    'Account'  = 'C:\boards\brd009\SEK\samples\Account\.specexplorerkit\out\AccountExploration.seexpl'
    'atsvc'    = 'C:\boards\brd009\SEK\samples\atsvc\.specexplorerkit\out\JobScheduler.seexpl'
    'SMB2'     = 'C:\boards\brd009\SEK\samples\SMB2\.specexplorerkit\out\Smb2Lifecycle.seexpl'
}

foreach ($name in $graphs.Keys) {
    $src = $graphs[$name]
    if (Test-Path $src) {
        dotnet $sek view $src --format html --out (Join-Path $outDir "$name.html") 2>&1
    }
}
