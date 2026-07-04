$sek = 'C:\boards\brd009\SEK\src\Sek.Cli\bin\Debug\sek.dll'
$proj = 'C:\boards\brd009\SEK\samples\Operators'
$machines = @(
  'Party','NoParty','SyncParallel','InterleavedParallel','SyncInterleavedParallel',
  'TightSequence','LooseSequence','Permutation','ZeroOrMore','OneOrMore','Optional',
  'BoundedRepetitionExact','BoundedRepetitionLeast','BoundedRepetitionRange',
  'AnyAction','RepetitionOfAnyAction','Negation','Truncation'
)
foreach ($m in $machines) {
    $out = dotnet $sek explore $m --project $proj 2>&1
    $line = ($out | Select-Object -First 1)
    '{0,-26} {1}' -f $m, $line
}
