# Runs the boss scenario once per boss level, each in a fresh game.
# Usage: tools\bosses.ps1 [level ...]
param([string[]]$Levels = @('Bridge', 'BeholderPit', 'TopClockTower', 'Throne', 'Giant', 'SwampHeart', 'GardenerStage', 'QueenArena', 'DeathArena', 'DookuArena', 'Observatory'))

. "$PSScriptRoot\hidden-game.ps1"

$root = Split-Path $PSScriptRoot
$core = "$GameDir\coremod"
$startup = "$core\core\host\startup"
$timeoutSeconds = 170
$report = "$root\build\boss-results.txt"

$env:RINGO_SCENARIO = 'boss'

function Read-Log {
    # Only this run's log: the previous one would report an old "DONE".
    $f = Get-ChildItem "$core\logs" -Filter 'log_2026*' | Where-Object { $_.CreationTime -ge $started } | Sort-Object CreationTime -Descending | Select-Object -First 1
    if (-not $f) { return @() }
    $s = [IO.File]::Open($f.FullName, 'Open', 'Read', 'ReadWrite,Delete')
    $r = New-Object IO.StreamReader($s)
    $t = $r.ReadToEnd()
    $r.Close()
    $t -split "`n"
}

foreach ($level in $Levels) {
    $env:RINGO_BOSS_LEVEL = $level
    $started = Get-Date
    $game = Start-TestGame $startup
    Start-Sleep 5
    $state = 'running'
    while ($state -eq 'running') {
        Start-Sleep 2
        if ($game.HasExited) { $state = 'CRASH' }
        elseif ((Read-Log) -match 'RINGO DONE') { $state = 'finished' }
        elseif (((Get-Date) - $started).TotalSeconds -gt $timeoutSeconds) { $state = 'TIMEOUT' }
    }
    $log = Read-Log
    Stop-TestGame $game
    Add-Content $report "=== $level : $state"
    $log | Where-Object { $_ -match 'RINGO (arrived|forcing|boss|after|travel|stuck|hero focus|t=)|\bERR\b|\bFTL\b|\bWRN\b' } | ForEach-Object { Add-Content $report $_.Trim() }
    if ($state -ne 'finished') {
        $log | Select-String 'Haxe!|hashlink exception' | Select-Object -First 6 | ForEach-Object { Add-Content $report ('  ' + $_.Line.Trim()) }
    }
    Write-Output "$level : $state"
}
Write-Output 'BOSSES FINISHED'
