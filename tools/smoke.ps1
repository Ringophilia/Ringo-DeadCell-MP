# Starts the game through the launcher, reports what happened, then closes it.
# Usage: tools\smoke.ps1 <bytecode.hlb> [seconds]
param([string]$Bytecode, [int]$Seconds = 25)

$root = Split-Path $PSScriptRoot
$dir = if ($env:DEADCELLS_DIR) { $env:DEADCELLS_DIR } else { 'C:\Program Files (x86)\Steam\steamapps\common\Dead Cells' }
$game = "$dir\deadcells.exe"
$log = Join-Path $root 'build\ringomp.log'
Remove-Item $log -ErrorAction SilentlyContinue

if (Get-Process deadcells -ErrorAction SilentlyContinue) {
    Write-Output 'deadcells is already running; not touching it'
    exit 2
}

$env:SteamAppId = '588650'
$env:SteamGameId = '588650'
$env:RINGO_BYTECODE = (Resolve-Path $Bytecode).Path
$env:RINGO_LOG = $log
$launcher = Start-Process (Join-Path $root 'build\ringo-launcher.exe') -ArgumentList "`"$game`"" -PassThru

$deadline = (Get-Date).AddSeconds($Seconds)
$title = ''
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 500
    $p = Get-Process deadcells -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($p -and $p.MainWindowTitle) { $title = $p.MainWindowTitle }
}

$p = Get-Process deadcells -ErrorAction SilentlyContinue | Select-Object -First 1
if ($p) {
    Write-Output "game: pid $($p.Id), responding=$($p.Responding), memory=$([math]::Round($p.WorkingSet64 / 1MB)) MB"
} else {
    Write-Output 'game: not running'
}
Write-Output "window title: '$title'"
Write-Output "launcher exited: $($launcher.HasExited)"
Write-Output '--- log'
if (Test-Path $log) { Get-Content $log } else { Write-Output '(no log)' }
if ($p) { Stop-Process -Id $p.Id -Force }
