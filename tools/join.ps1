# A player joining a run in progress, crashing, and coming back. The host plays alone
# first; the client starts later, and its second life is a new process.
# Usage: tools\join.ps1 [seconds]
param([int]$Seconds = 300)

. "$PSScriptRoot\hidden-game.ps1"

$core = "$GameDir\coremod"
$startup = "$core\core\host\startup"
$hostHeadStart = 45

$env:RINGO_SCENARIO = 'join'
$env:RINGO_TRANSPORT = 'steam'
$env:RINGO_LATE = '1'
if (-not $env:RINGO_SEED) { $env:RINGO_SEED = '555123' }

$started = Get-Date
$env:RINGO_ROLE = 'host'
$hostGame = Start-TestGame $startup
Start-Sleep $hostHeadStart

$env:RINGO_ROLE = 'client'
$first = Start-TestGame $startup
while (-not $first.HasExited -and ((Get-Date) - $started).TotalSeconds -lt $Seconds) { Start-Sleep 2 }
Write-Output "first client gone after $([math]::Round(((Get-Date) - $started).TotalSeconds))s"

$env:RINGO_SECOND_LIFE = '1'
$second = Start-TestGame $startup
while (((Get-Date) - $started).TotalSeconds -lt $Seconds) {
    Start-Sleep 3
    $done = @(Get-ChildItem "$core\logs" -Filter 'log_2026*' | Where-Object { $_.CreationTime -ge $started } | Where-Object { (Get-Content $_.FullName -Encoding UTF8 -Raw) -match 'RINGO DONE' }).Count
    if ($done -ge 2 -or $second.HasExited -or $hostGame.HasExited) { break }
}
Start-Sleep 1
$i = 0
foreach ($log in Get-ChildItem "$core\logs" -Filter 'log_2026*' | Where-Object { $_.CreationTime -ge $started } | Sort-Object CreationTime) {
    Write-Output "===== log $i"
    Get-Content $log.FullName -Encoding UTF8 | Where-Object { $_ -match 'RINGO|\bERR\b|\bFTL\b|hashlink exception' } | ForEach-Object { $_.Trim() -replace '^\[(\d\d:\d\d:\d\d) \w+\]\[RingoMod\] RINGO ', '$1 ' } | Select-Object -Last 45
    $i++
}
Stop-TestGame @($hostGame, $first, $second)
