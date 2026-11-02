# Tests every item on the remote hero, restarting the game after each crash or hang.
# Usage: tools\items.ps1 [-Items list.txt] [-Results out.tsv] [-Subject remote|main] [-MaxRuns n]
param(
    [string]$Items = 'items.txt',
    [string]$Results = 'item-results.tsv',
    [string]$Subject = 'remote',
    [int]$MaxRuns = 40
)

. "$PSScriptRoot\hidden-game.ps1"

$root = Split-Path $PSScriptRoot
$core = "$GameDir\coremod"
$startup = "$core\core\host\startup"
$hangSeconds = 45
$bootSeconds = 90

$env:RINGO_SCENARIO = 'items'
$env:RINGO_SUBJECT = $Subject
$env:RINGO_ITEMS = "$root\build\$Items"
$env:RINGO_RESULTS = "$root\build\$Results"
$env:RINGO_CURRENT = "$root\build\item-current.txt"
Remove-Item $env:RINGO_CURRENT -ErrorAction SilentlyContinue

function Read-Log {
    $f = Get-ChildItem "$core\logs" -Filter 'log_2026*' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    $s = [IO.File]::Open($f.FullName, 'Open', 'Read', 'ReadWrite,Delete')
    $r = New-Object IO.StreamReader($s)
    $t = $r.ReadToEnd()
    $r.Close()
    $t
}

for ($run = 1; $run -le $MaxRuns; $run++) {
    $started = Get-Date
    Remove-Item $env:RINGO_CURRENT -ErrorAction SilentlyContinue
    $p = Start-TestGame $startup
    Start-Sleep 5
    $lastSeen = ''
    $lastChange = Get-Date
    $state = 'running'
    while ($state -eq 'running') {
        Start-Sleep 2
        $current = if (Test-Path $env:RINGO_CURRENT) { Get-Content $env:RINGO_CURRENT -Raw } else { '' }
        if ($current -ne $lastSeen) { $lastSeen = $current; $lastChange = Get-Date }
        if ($p.HasExited) { $state = 'crash' }
        elseif ((Read-Log) -match 'RINGO DONE') { $state = 'done' }
        elseif ($current -and ((Get-Date) - $lastChange).TotalSeconds -gt $hangSeconds) { $state = 'hang' }
        elseif (-not $current -and ((Get-Date) - $started).TotalSeconds -gt $bootSeconds) { $state = 'bootfail' }
    }
    Stop-TestGame $p
    $count = if (Test-Path $env:RINGO_RESULTS) { (Get-Content $env:RINGO_RESULTS).Count } else { 0 }
    Write-Output "run $run ended: $state at '$lastSeen', $count results"
    if ($state -eq 'done') { break }
    if ($state -eq 'bootfail') { (Read-Log) -split "`n" | Select-Object -Last 15; break }

    $done = @{}
    if (Test-Path $env:RINGO_RESULTS) { Get-Content $env:RINGO_RESULTS | ForEach-Object { $done[($_ -split "`t")[0]] = 1 } }
    if ($lastSeen -and -not $done.ContainsKey($lastSeen)) {
        $category = (Get-Content $env:RINGO_ITEMS | Where-Object { ($_ -split "`t")[0] -eq $lastSeen } | ForEach-Object { ($_ -split "`t")[1] })
        $stack = ((Read-Log) -split "`n" | Select-String 'Haxe!|hashlink exception' | Select-Object -First 4 | ForEach-Object { $_.Line.Trim() -replace '^at ', '' }) -join ' <- '
        Add-Content $env:RINGO_RESULTS "$lastSeen`t$category`t$state`t0`t$stack"
    }
}
Write-Output 'ITEMS FINISHED'
