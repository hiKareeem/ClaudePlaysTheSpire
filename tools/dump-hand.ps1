. .\autopilot-lib.ps1
$state = Read-State
foreach ($c in $state.combat.hand.cards) {
    $play = if ($c.isPlayable) { "Y" } else { "N" }
    Write-Output "[$($c.handIndex)] $($c.title) | $($c.energyCost)e $($c.type) | play=$play | tgt=$($c.targetType)"
}
