# Starts the DCCM game with a test scenario, waits for it to finish, prints its log lines.
# Usage: tools\spike.ps1 [scenario] [seconds]
param([string]$Scenario = 'm2', [int]$Seconds = 150)

. "$PSScriptRoot\hidden-game.ps1"

$env:RINGO_SCENARIO = $Scenario

$core = "$GameDir\coremod"
$startup = "$core\core\host\startup"

function Read-Log {
    $f = Get-ChildItem "$core\logs" -Filter 'log_2026*' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    $s = [IO.File]::Open($f.FullName, 'Open', 'Read', 'ReadWrite,Delete')
    $r = New-Object IO.StreamReader($s)
    $t = $r.ReadToEnd()
    $r.Close()
    $t -split "`n"
}

$t0 = Get-Date
$p = Start-TestGame $startup
$crashed = $false
while (((Get-Date) - $t0).TotalSeconds -lt $Seconds) {
    Start-Sleep 3
    if ($p.HasExited) { $crashed = $true; break }
    if ((Read-Log) -match 'RINGO DONE') { break }
}
$elapsed = [math]::Round(((Get-Date) - $t0).TotalSeconds)
Start-Sleep 1
$log = Read-Log
Write-Output "elapsed ${elapsed}s, process gone=$crashed"
$log | Where-Object { $_ -match 'RINGO|\bERR\b|\bFTL\b|Exception' } | Select-Object -Last 50
if ($crashed) { Write-Output '--- tail'; $log | Select-Object -Last 25 }
if (-not $env:RINGO_KEEP) { Stop-TestGame $p }
