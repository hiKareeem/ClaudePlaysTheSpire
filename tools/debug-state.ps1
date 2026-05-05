. (Join-Path $PSScriptRoot '..\autopilot-lib.ps1')
$paths = Get-IpcPaths
$j = Get-Content -Raw $paths.StateFile | ConvertFrom-Json
"player.block=$($j.combat.player.block)"
"player.hp=$($j.combat.player.hp)"
$keys = $j.PSObject.Properties.Name
"top keys: $($keys -join ', ')"
if ($j.PSObject.Properties.Match('chooseACardScreen').Count -gt 0) {
    "chooseACard.active=$($j.chooseACardScreen.active)"
}
if ($j.PSObject.Properties.Match('handSelectScreen').Count -gt 0) {
    "handSelect.active=$($j.handSelectScreen.active)"
}
if ($j.PSObject.Properties.Match('targetingScreen').Count -gt 0) {
    $j.targetingScreen | ConvertTo-Json -Depth 3 -Compress
}
"--combat keys--"
$j.combat.PSObject.Properties.Name -join ', '
