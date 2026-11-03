# Starts two games the way players do, with no test scenario. The first opens a room,
# the second lists the rooms; both print what their room panel shows. A real join
# needs a second Steam account. The second also gets a Steam-style join argument.
# Each saves a picture of its lobby to build\lobby-room.png and build\lobby-list.png.
# Usage: tools\lobby.ps1 [seconds]
param([int]$Seconds = 40)

. "$PSScriptRoot\hidden-game.ps1"

$core = "$GameDir\coremod"
$startup = "$core\core\host\startup"

$env:RINGO_PANEL_LOG = '1'
Remove-Item Env:RINGO_SCENARIO, Env:RINGO_ROLE -ErrorAction SilentlyContinue
$started = Get-Date
$pictures = Join-Path (Split-Path $PSScriptRoot) 'build'
$env:RINGO_ROOMS = 'host'
$env:RINGO_SNAPSHOT = "$pictures\lobby-room.png"
$first = Start-TestGame $startup
Start-Sleep 12
$env:RINGO_ROOMS = 'list'
$env:RINGO_SNAPSHOT = "$pictures\lobby-list.png"
$second = Start-TestGame $startup -Arguments '+connect_lobby 12345'
while (((Get-Date) - $started).TotalSeconds -lt $Seconds -and -not $first.HasExited -and -not $second.HasExited) {
    Start-Sleep 2
}
Write-Output "crashed: first=$($first.HasExited) second=$($second.HasExited)"
Stop-TestGame @($first, $second)
Remove-Item Env:RINGO_SNAPSHOT, Env:RINGO_ROOMS -ErrorAction SilentlyContinue
foreach ($log in Get-ChildItem "$core\logs" -Filter 'log_2026*' | Where-Object { $_.CreationTime -ge $started } | Sort-Object CreationTime) {
    Write-Output "===== $($log.Name)"
    $lines = Get-Content $log.FullName -Encoding UTF8
    $start = ($lines | Select-String 'hero focus' | Select-Object -First 1).LineNumber
    $lines[$start..($lines.Count - 1)] | Where-Object { $_ -notmatch '\[(Game|FsPak|EventSystem)\]' } | Select-Object -Last 40
}
