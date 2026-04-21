param([string]$Filter)
. (Join-Path $PSScriptRoot '..\autopilot-lib.ps1')
$state = Read-State
if ($Filter -eq 'screen') {
    Write-Output "screen=$($state.screen) revision=$($state.revision)"
    if ($state.run) {
        Write-Output "hp=$($state.run.currentHp)/$($state.run.maxHp) gold=$($state.run.gold)"
    }
} elseif ($Filter -eq 'combat') {
    $c = $state.combat
    if (-not $c) { Write-Output "No combat"; return }
    Write-Output "round=$($c.roundNumber) energy=$($c.energy)/$($c.maxEnergy) side=$($c.currentSide)"
    if ($c.player) { Write-Output "player hp=$($c.player.currentHp)/$($c.player.maxHp) block=$($c.player.block)" }
    if ($c.enemies) {
        for ($i = 0; $i -lt $c.enemies.Count; $i++) {
            $e = $c.enemies[$i]
            if ($e) { Write-Output "enemy[$i] $($e.name) hp=$($e.currentHp)/$($e.maxHp) block=$($e.block)" }
        }
    }
    if ($c.hand -and $c.hand.cards) {
        for ($i = 0; $i -lt $c.hand.cards.Count; $i++) {
            $card = $c.hand.cards[$i]
            if ($card) { Write-Output "hand[$i] $($card.title) cost=$($card.effectiveEnergyCost) playable=$($card.isPlayable)" }
        }
    }
} elseif ($Filter -eq 'full') {
    $state | ConvertTo-Json -Depth 10
} else {
    Write-Output "screen=$($state.screen) revision=$($state.revision)"
    if ($state.run) {
        Write-Output "hp=$($state.run.currentHp)/$($state.run.maxHp) gold=$($state.run.gold)"
        if ($state.run.relics) { Write-Output "relics: $($state.run.relics | ForEach-Object { $_.title })" }
        if ($state.run.potions) { 
            for ($i = 0; $i -lt $state.run.potions.Count; $i++) {
                $p = $state.run.potions[$i]
                if ($p) { Write-Output "potion[$i] $($p.title)" } else { Write-Output "potion[$i] (empty)" }
            }
        }
    }
    if ($state.map -and $state.map.available) {
        Write-Output "map nodes: $($state.map.available | ForEach-Object { "($_.col,$($_.row))=$($_.pointType)" })"
    }
    if ($state.rewards) {
        for ($i = 0; $i -lt $state.rewards.Count; $i++) {
            $r = $state.rewards[$i]
            Write-Output "reward[$i] kind=$($r.kind) index=$($r.index)"
        }
    }
}
