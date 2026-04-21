. (Join-Path $PSScriptRoot '..\autopilot-lib.ps1')
$s = Read-State
$h = $s.combat.hand
Write-Host "ENERGY: $($s.combat.energy)"
if ($h.cards) {
    Write-Host "HAND ($($h.cards.Count)):"
    for ($i=0; $i -lt $h.cards.Count; $i++) {
        $c = $h.cards[$i]
        Write-Host "  [$i] $($c.title) E:$($c.effectiveEnergyCost) play=$($c.isPlayable)"
    }
} elseif ($h -is [array]) {
    Write-Host "HAND ($($h.Count)):"
    for ($i=0; $i -lt $h.Count; $i++) {
        $c = $h[$i]
        Write-Host "  [$i] $($c.title) E:$($c.effectiveEnergyCost) play=$($c.isPlayable)"
    }
}
$enemies = $s.combat.enemies
Write-Host "ENEMIES ($($enemies.Count)):"
for ($i=0; $i -lt $enemies.Count; $i++) {
    $e = $enemies[$i]
    Write-Host "  [$i] $($e.name) HP:$($e.currentHp)/$($e.maxHp) Block:$($e.block)"
    if ($e.powers) { Write-Host "    Powers: $($e.powers | ForEach-Object { $_.id })" }
    if ($e.intents) { Write-Host "   Intents: $($e.intents | ForEach-Object { $_.type } )" }
}
