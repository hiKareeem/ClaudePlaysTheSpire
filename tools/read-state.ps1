param([string]$Filter)
# General state inspector. Prefer read-combat for combats, read-map for map screen,
# list-rewards for rewards screen, list-event/list-rest/list-grid/list-cards for
# their respective pickers. This script is the fallback overview.
#
# Filters:
#   (none)  : HP/gold/screen/potions/relics/map next/rewards summary
#   screen  : just the screen/revision + HP
#   combat  : terse combat (use tools/read-combat.ps1 for full combat view)
#   full    : dumps entire state as JSON (very large)
. (Join-Path $PSScriptRoot '..\autopilot-lib.ps1')
$state = Read-State

function ScreenLine($s) {
    $name = if ($s.screen -and $s.screen.name) { $s.screen.name } else { 'null' }
    Write-Output "screen=$name  revision=$($s.revision)"
}

if ($Filter -eq 'screen') {
    ScreenLine $state
    if ($state.run) { Write-Output "hp=$($state.run.currentHp)/$($state.run.maxHp)  gold=$($state.run.gold)" }
    return
}
if ($Filter -eq 'combat') {
    $c = $state.combat
    if (-not $c) { Write-Output "No combat"; return }
    Write-Output "round=$($c.roundNumber) side=$($c.currentSide) energy=$($c.energy)/$($c.maxEnergy)"
    if ($c.player) { Write-Output "player hp=$($c.player.currentHp)/$($c.player.maxHp) block=$($c.player.block)" }
    if ($c.enemies) {
        for ($i = 0; $i -lt $c.enemies.Count; $i++) {
            $e = $c.enemies[$i]; if (-not $e) { continue }
            $nm = if ($e.name) { $e.name } else { $e.title }
            Write-Output "enemy[$i] $nm hp=$($e.currentHp)/$($e.maxHp) block=$($e.block)"
        }
    }
    if ($c.hand -and $c.hand.cards) {
        for ($i = 0; $i -lt $c.hand.cards.Count; $i++) {
            $card = $c.hand.cards[$i]; if (-not $card) { continue }
            Write-Output "hand[$i] $($card.title) cost=$($card.effectiveEnergyCost) playable=$($card.isPlayable) target=$($card.targetType)"
        }
    }
    Write-Output "(use tools/read-combat.ps1 for intents, powers, descriptions)"
    return
}
if ($Filter -eq 'full') { $state | ConvertTo-Json -Depth 10; return }

# Default view
ScreenLine $state
if ($state.run) {
    Write-Output "hp=$($state.run.currentHp)/$($state.run.maxHp)  gold=$($state.run.gold)"
    if ($state.run.relics) {
        $names = $state.run.relics | ForEach-Object { $_.title }
        Write-Output ("relics: " + ($names -join ', '))
    }
    if ($state.run.potions) {
        for ($i = 0; $i -lt $state.run.potions.Count; $i++) {
            $p = $state.run.potions[$i]
            if ($p) { Write-Output "potion[$i] $($p.title)" } else { Write-Output "potion[$i] (empty)" }
        }
    }
}
if ($state.map -and $state.map.currentCoord) {
    Write-Output "map: at (col=$($state.map.currentCoord.col) row=$($state.map.currentCoord.row))"
    if ($state.map.available) {
        $next = $state.map.available | ForEach-Object { "(c=$($_.col),r=$($_.row))$($_.pointType)" }
        Write-Output ("  next: " + ($next -join '  '))
    }
}
if ($state.rewards -and $state.rewards.Count -gt 0) {
    Write-Output "--- rewards (see tools/list-rewards.ps1 for details) ---"
    for ($i = 0; $i -lt $state.rewards.Count; $i++) {
        $r = $state.rewards[$i]
        $detail = switch ($r.kind) {
            'Gold'   { "amount=$($r.amount)" }
            'Potion' { if ($r.potion) { $r.potion.title } else { '?' } }
            'Relic'  { if ($r.relic)  { $r.relic.title  } else { '?' } }
            'Card'   { "$( if ($r.cards) { $r.cards.Count } else { 0 } ) options" }
            default  { '' }
        }
        Write-Output "  reward[$i] kind=$($r.kind) idx=$($r.index) $detail"
    }
}
