. .\autopilot-lib.ps1
$state = Read-State
Write-Output "=== STATE ==="
Write-Output "Screen: $($state.screen.name)"
Write-Output "HP: $($state.run.currentHp)/$($state.run.maxHp)  Gold: $($state.run.gold)  Floor: $($state.run.actFloor)"

if ($null -ne $state.combat) {
    Write-Output ""
    Write-Output "--- ENERGY ---"
    Write-Output "Energy: $($state.combat.energy)/$($state.combat.maxEnergy)  Round: $($state.combat.roundNumber)"
    
    Write-Output ""
    Write-Output "--- PLAYER ---"
    Write-Output "  HP: $($state.combat.player.currentHp)/$($state.combat.player.maxHp)  Block: $($state.combat.player.block)"
    if ($state.combat.player.powers.Count -gt 0) {
        $pp = ($state.combat.player.powers | ForEach-Object { "$($_.title)($($_.amount))" }) -join ", "
        Write-Output "  Powers: $pp"
    }
    
    Write-Output ""
    Write-Output "--- ENEMIES ---"
    foreach ($enemy in $state.combat.enemies) {
        $status = if ($enemy.isAlive) { "ALIVE" } else { "DEAD" }
        Write-Output "  $($enemy.name) [$status] | HP:$($enemy.currentHp)/$($enemy.maxHp) | Block:$($enemy.block)"
        if ($enemy.powers.Count -gt 0) {
            $pows = ($enemy.powers | ForEach-Object { "$($_.title)($($_.amount))" }) -join ", "
            Write-Output "    Powers: $pows"
        }
        if ($enemy.intents.Count -gt 0) {
            foreach ($intent in $enemy.intents) {
                Write-Output "    Intent: $($intent.title) type=$($intent.intentType) dmg=$($intent.damage) label=$($intent.label)"
            }
        }
    }
    
    Write-Output ""
    Write-Output "--- HAND ---"
    if ($null -ne $state.combat.hand -and $null -ne $state.combat.hand.cards) {
        foreach ($card in $state.combat.hand.cards) {
            $play = if ($card.isPlayable) { "Y" } else { "N" }
            Write-Output "  [$($card.handIndex)] $($card.title) | $($card.energyCost)e $($card.type) | play=$play | tgt=$($card.targetType)"
        }
    }
}

if ($null -ne $state.event) {
    Write-Output ""
    Write-Output "--- EVENT ---"
    Write-Output "  $($state.event.title) (finished=$($state.event.isFinished))"
    foreach ($opt in $state.event.options) {
        Write-Output "  [$($opt.index)] $($opt.title): $($opt.description)"
    }
}

if ($null -ne $state.rewards -and $state.rewards.Count -gt 0) {
    Write-Output ""
    Write-Output "--- REWARDS ---"
    $i = 0
    foreach ($r in $state.rewards) {
        Write-Output "  [$i] $($r.type): $($r.title) - $($r.description)"
        $i++
    }
}

if ($null -ne $state.cardRewardOptions -and $state.cardRewardOptions.Count -gt 0) {
    Write-Output ""
    Write-Output "--- CARD REWARD OPTIONS ---"
    foreach ($opt in $state.cardRewardOptions) {
        Write-Output "  [$($opt.index)] $($opt.title) | $($opt.rarity) | $($opt.energyCost)e $($opt.type) | $($opt.description)"
    }
}
