# Starts a host and two or three clients on this machine, waits for all of them,
# prints each one's log. Needs Steam running: the games link over its sockets.
# Usage: tools\crowd.ps1 [players] [seconds]
param([int]$Players = 3, [int]$Seconds = 300)

. "$PSScriptRoot\hidden-game.ps1"

$core = "$GameDir\coremod"
$startup = "$core\core\host\startup"
$pause = 6

$env:RINGO_SCENARIO = 'crowd'
$env:RINGO_PLAYERS = "$Players"
$env:RINGO_LATE = '1'
$started = Get-Date

$games = @()
$env:RINGO_ROLE = 'host'
$games += Start-TestGame $startup
for ($i = 1; $i -lt $Players; $i++) {
    Start-Sleep $pause
    $env:RINGO_ROLE = 'client'
    $games += Start-TestGame $startup
}

function Read-One($file) {
    $s = [IO.File]::Open($file.FullName, 'Open', 'Read', 'ReadWrite,Delete')
    $r = New-Object IO.StreamReader($s)
    $t = $r.ReadToEnd()
    $r.Close()
    $t -split "`n"
}

function Get-Logs { Get-ChildItem "$core\logs" -Filter 'log_2026*' | Where-Object { $_.CreationTime -ge $started } | Sort-Object CreationTime }

$finished = $false
while (((Get-Date) - $started).TotalSeconds -lt $Seconds) {
    Start-Sleep 3
    $done = @(Get-Logs | Where-Object { (Read-One $_) -match 'RINGO DONE' }).Count
    if ($done -ge $Players) { $finished = $true; break }
    if (@($games | Where-Object { $_.HasExited }).Count -gt 0) { break }
}
Start-Sleep 1
Write-Output "finished=$finished elapsed=$([math]::Round(((Get-Date) - $started).TotalSeconds))s"
$i = 0
foreach ($log in Get-Logs) {
    Write-Output "===== log $i"
    Read-One $log | Where-Object { $_ -match 'RINGO|\bERR\b|\bFTL\b|hashlink exception|Haxe!' } | ForEach-Object { $_.Trim() -replace '^\[(\d\d:\d\d:\d\d) \w+\]\[RingoMod\] RINGO ', '$1 ' } | Select-Object -Last 80
    $i++
}
foreach ($name in 'RINGO_SCENARIO', 'RINGO_PLAYERS', 'RINGO_LATE', 'RINGO_ROLE') { Remove-Item "Env:$name" -ErrorAction SilentlyContinue }
if (-not $env:RINGO_KEEP) { Stop-TestGame $games }
