# Card reward inspector. Use after combat/elite/boss when screen=RewardsRoom with a Card reward,
# or during shop/event card-pick screens where $s.cardRewardOptions is populated.
# Pair with send-cmd SelectReward { rewardIndex=N } then SelectCardOption { cardIndex=M } where N is rewards[i].index
# and M is the [i] shown below.
. (Join-Path $PSScriptRoot '..\autopilot-lib.ps1')
$s = Read-State

if (-not $s.cardRewardOptions -or -not $s.cardRewardOptions.cards) {
    Write-Output "(no cardRewardOptions on current screen: $($s.screen.name))"
    return
}

Write-Output "Screen: $($s.screen.name)"
Write-Output "--- CARD OPTIONS ---"
$i = 0
foreach ($opt in $s.cardRewardOptions.cards) {
    $c = $opt.card
    if (-not $c) { Write-Output "  [$i] (null card)"; $i++; continue }
    $upg = if ($c.isUpgraded) { '+' } else { '' }
    $cost = if ($null -ne $c.energyCost -and $c.energyCost -ge 0) { "$($c.energyCost)E" } else { '?E' }
    Write-Output ("  [{0}] {1}{2}  {3}  {4}  type={5}" -f $i, $c.title, $upg, $c.rarity, $cost, $c.type)
    if ($c.description) { Write-Output "       $($c.description)" }
    $i++
}
