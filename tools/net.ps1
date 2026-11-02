# Starts a host and a client game on this machine, waits for both, prints each side's log.
# Usage: tools\net.ps1 [seconds] [scenario]
param([int]$Seconds = 110, [string]$Scenario = 'net')

. "$PSScriptRoot\hidden-game.ps1"

$root = Split-Path $PSScriptRoot
$core = "$GameDir\coremod"
$startup = "$core\core\host\startup"
$pause = 6

$env:RINGO_SCENARIO = $Scenario
$started = Get-Date

$env:RINGO_ROLE = 'host'
$hostGame = Start-TestGame $startup
Start-Sleep $pause
$env:RINGO_ROLE = 'client'
$clientGame = Start-TestGame $startup

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
    $logs = @(Get-Logs)
    $done = @($logs | Where-Object { (Read-One $_) -match 'RINGO DONE' }).Count
    if ($done -ge 2) { $finished = $true; break }
    if ($hostGame.HasExited -or $clientGame.HasExited) { break }
}
Start-Sleep 1
Write-Output "finished=$finished elapsed=$([math]::Round(((Get-Date) - $started).TotalSeconds))s"
$i = 0
foreach ($log in Get-Logs) {
    Write-Output "===== log $i"
    Read-One $log | Where-Object { $_ -match 'RINGO|\bERR\b|\bFTL\b|hashlink exception|Haxe!' } | ForEach-Object { $_.Trim() -replace '^\[(\d\d:\d\d:\d\d) \w+\]\[RingoMod\] RINGO ', '$1 ' } | Select-Object -Last 70
    $i++
}
if (-not $env:RINGO_KEEP) { Stop-TestGame @($hostGame, $clientGame) }
