$sek = 'C:\boards\brd009\SEK\src\Sek.Cli\bin\Debug\sek.dll'
function E($label, $machine, $proj) {
    $line = dotnet $sek explore $machine --project $proj 2>&1 | Select-Object -First 1
    '{0,-24}: {1}' -f $label, $line
}
E 'Operators/Party'      'Party'                'C:\boards\brd009\SEK\samples\Operators'
E 'Operators/SyncPar'    'SyncParallel'         'C:\boards\brd009\SEK\samples\Operators'
E 'PG/Product'           'Product'              'C:\boards\brd009\SEK\samples\ParameterGeneration'
E 'PG/Pairwise'          'Pairwise'             'C:\boards\brd009\SEK\samples\ParameterGeneration'
E 'PG/Constraint'        'Constraint'           'C:\boards\brd009\SEK\samples\ParameterGeneration'
E 'Account'              'AccountExploration'   'C:\boards\brd009\SEK\samples\Account'
E 'PubSub'               'PubSubExploration'    'C:\boards\brd009\SEK\samples\PubSub'
E 'atsvc'                'JobScheduler'         'C:\boards\brd009\SEK\samples\atsvc'
E 'chat'                 'ChatProtocol'         'C:\boards\brd009\SEK\samples\chat'
E 'SMB2'                 'Smb2Lifecycle'        'C:\boards\brd009\SEK\samples\SMB2'
E 'Sailboat'             'Voyage'               'C:\boards\brd009\SEK\samples\Sailboat'
E 'TPC-C'                'TpccExploration'      'C:\boards\brd009\SpecExplorer'
