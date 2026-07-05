$sek = 'C:\boards\brd009\SEK\src\Sek.Cli\bin\Debug\sek.dll'
$proj = 'C:\boards\brd009\SEK\samples\Sailboat'
foreach ($m in 'ShootCompleter','Point','BoundedShoot1','BoundedShoot','BoundedShoot2','PointAndShoot','PointAndShoot2','TestSuite') {
    $line = dotnet $sek explore $m --project $proj 2>&1 | Select-Object -First 1
    Write-Host ('{0,-16}: {1}' -f $m, $line)
}
