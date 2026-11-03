# Starts one game that begins a run, opens the pause menu, opens the lobby from its
# Multiplayer entry and goes back, with no writes to the save. It saves a picture of
# each step to build\pause-menu.png, build\pause-lobby.png and build\pause-back.png.
# Usage: tools\pause.ps1 [seconds]
param([int]$Seconds = 120)

. "$PSScriptRoot\hidden-game.ps1"

$core = "$GameDir\coremod"
$startup = "$core\core\host\startup"
$pictures = Join-Path (Split-Path $PSScriptRoot) 'build'

Remove-Item Env:RINGO_SCENARIO, Env:RINGO_ROLE -ErrorAction SilentlyContinue
$env:RINGO_ROOMS = 'pause'
$env:RINGO_SNAPSHOT = $pictures
$started = Get-Date
$game = Start-TestGame $startup
$log = $null
while (((Get-Date) - $started).TotalSeconds -lt $Seconds -and -not $game.HasExited) {
    Start-Sleep 2
    $log = Get-ChildItem "$core\logs" -Filter 'log_2026*' | Where-Object { $_.CreationTime -ge $started } | Select-Object -Last 1
    if ($log -and (Select-String -Path $log.FullName -Pattern 'pause result' -Quiet)) {
        break
    }
}
Write-Output "crashed: $($game.HasExited)"
Stop-TestGame @($game)
Remove-Item Env:RINGO_SNAPSHOT, Env:RINGO_ROOMS -ErrorAction SilentlyContinue
if ($log) {
    Get-Content $log.FullName -Encoding UTF8 | Select-String 'RINGO pause|snapshot|ERR|xception'
}
