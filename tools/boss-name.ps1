. (Join-Path $PSScriptRoot '..\autopilot-lib.ps1')
# Diagnostic: dump boss-identity fields from the current state.json snapshot.
# The supported fields (bridge >= v0.2.0) are map.bossId, map.bossName, map.bossCoord.
# read-map.ps1 surfaces these inline; this script exists for raw inspection
# and for verifying field availability after a fresh boot.
$paths = Get-IpcPaths
$j = Get-Content -Raw $paths.StateFile | ConvertFrom-Json
if (-not $j.map) { Write-Host 'No active map.'; return }
"map.bossId   = $($j.map.bossId)"
"map.bossName = $($j.map.bossName)"
if ($j.map.bossCoord) {
    "map.bossCoord = col=$($j.map.bossCoord.col) row=$($j.map.bossCoord.row)"
} else {
    "map.bossCoord = <null>"
}
