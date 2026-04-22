# Post-combat / chest / boss rewards inspector. Use when $s.rewards is populated
# (screen=RewardsRoom, CombatRewardsRoom, TreasureRoom, etc).
# All reward kinds use the unified command surface:
#   - Gold / Relic / Potion: send SelectReward { rewardPosition=N }
#   - Card: send SelectReward { rewardPosition=N } then SelectCardOption { cardIndex=M }
#   - Skip one: send SkipReward { rewardPosition=N }  (works for all kinds)
#   - Skip all: send SkipAllRewards  (auto-closes panel → RewardsClosed)
# NOTE: Prefer `rewardPosition` (= rewards[i].position, always unique, shown as "pos"
#       below). Legacy `rewardIndex` (= rewards[i].index / RewardsSetIndex, shown as
#       "idx") still works but is NOT unique when multiple rewards share a set —
#       e.g. event-procured potions often share idx=2.
# NOTE: Potion rewards have no canSkip field in state.json but SkipReward still works on them.
# NOTE: Do NOT guess per-kind command names (SelectGold/SelectPotionReward/etc) - they do not exist.
. (Join-Path $PSScriptRoot '..\autopilot-lib.ps1')
$s = Read-State

if (-not $s.rewards -or $s.rewards.Count -eq 0) {
    Write-Output "(no rewards on current screen: $($s.screen.name))"
    return
}

Write-Output "Screen: $($s.screen.name)"
Write-Output "HP: $($s.run.currentHp) / $($s.run.maxHp)   Gold: $($s.run.gold)"
$occ = 0
if ($s.run.potions) { foreach ($p in $s.run.potions) { if ($p) { $occ++ } } }
$cap = if ($s.run.potions) { $s.run.potions.Count } else { 0 }
Write-Output "Potion slots: $occ / $cap"
Write-Output "--- REWARDS (prefer 'pos' for rewardPosition) ---"
for ($i = 0; $i -lt $s.rewards.Count; $i++) {
    $r = $s.rewards[$i]
    $pos = if ($r.PSObject.Properties['position']) { $r.position } else { $i }
    $flags = @()
    if ($r.PSObject.Properties['canSkip'])   { $flags += "canSkip=$($r.canSkip)" }
    if ($r.PSObject.Properties['canReroll']) { $flags += "canReroll=$($r.canReroll)" }
    $flagStr = if ($flags.Count) { '  ' + ($flags -join ' ') } else { '' }
    switch ($r.kind) {
        'Gold'   { Write-Output ("  pos={0} idx={1}  kind=Gold    amount={2}" -f $pos, $r.index, $r.amount) }
        'Potion' {
            $pt = if ($r.potion) { $r.potion.title } else { '?' }
            Write-Output ("  pos={0} idx={1}  kind=Potion  {2}{3}" -f $pos, $r.index, $pt, $flagStr)
            if ($r.potion.description) { Write-Output "       $($r.potion.description)" }
        }
        'Relic'  {
            $rt = if ($r.relic) { $r.relic.title } else { '?' }
            Write-Output ("  pos={0} idx={1}  kind=Relic   {2}  rarity={3}{4}" -f $pos, $r.index, $rt, $r.rarity, $flagStr)
            if ($r.relic.description) { Write-Output "       $($r.relic.description)" }
        }
        'Card'   {
            $n = if ($r.cards) { $r.cards.Count } else { 0 }
            Write-Output ("  pos={0} idx={1}  kind=Card    options={2}{3}" -f $pos, $r.index, $n, $flagStr)
            if ($r.cards) {
                $j = 0
                foreach ($c in $r.cards) {
                    $upg = if ($c.isUpgraded) { '+' } else { '' }
                    Write-Output ("       cardIdx={0} {1}{2} ({3})" -f $j, $c.title, $upg, $c.rarity)
                    $j++
                }
            }
        }
        default  { Write-Output ("  pos={0} idx={1}  kind={2}{3}" -f $pos, $r.index, $r.kind, $flagStr) }
    }
}
