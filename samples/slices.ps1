$sek = 'C:\boards\brd009\SEK\src\Sek.Cli\bin\Debug\sek.dll'
function E($label, $machine, $proj) {
    $line = dotnet $sek explore $machine --project $proj 2>&1 | Select-Object -First 1
    '{0,-22}: {1}' -f $label, $line
}
E 'Account full'    'AccountExploration'  'C:\boards\brd009\SEK\samples\Account'
E 'Account slice'   'SlicedAccount'       'C:\boards\brd009\SEK\samples\Account'
E 'PubSub full'     'PubSubExploration'   'C:\boards\brd009\SEK\samples\PubSub'
E 'PubSub slice'    'TwoSubscribersSlice' 'C:\boards\brd009\SEK\samples\PubSub'
E 'chat full'       'ChatProtocol'        'C:\boards\brd009\SEK\samples\chat'
E 'chat slice'      'ChatSlice'           'C:\boards\brd009\SEK\samples\chat'
E 'SMB2 full'       'Smb2Lifecycle'       'C:\boards\brd009\SEK\samples\SMB2'
E 'SMB2 slice'      'SyncSession'         'C:\boards\brd009\SEK\samples\SMB2'
E 'atsvc full'      'JobScheduler'        'C:\boards\brd009\SEK\samples\atsvc'
E 'atsvc slice'     'ManagedJobs'         'C:\boards\brd009\SEK\samples\atsvc'
