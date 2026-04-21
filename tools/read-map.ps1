. (Join-Path $PSScriptRoot '..\autopilot-lib.ps1')
$state = Read-State
$state.combat.hand.cards | ForEach-Object { "$($_.handIndex): $($_.title) ($($_.effectiveEnergyCost)E)" }
Write-Output "---"
Write-Output "Energy: $($state.combat.energy)/$($state.combat.maxEnergy)"
Write-Output "Stars: $($state.combat.stars)"
Write-Output "Player HP: $($state.combat.player.currentHp)/$($state.combat.player.maxHp) Block: $($state.combat.player.block)"
$state.combat.enemies | ForEach-Object { "$($_.name) HP: $($_.currentHp)/$($_.maxHp) Block: $($_.block)" }
$state.combat.player.powers | ForEach-Object { "Power: $($_.name) x$($_.amount)" }
