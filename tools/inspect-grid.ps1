. (Join-Path $PSScriptRoot '..\autopilot-lib.ps1')
$p = Get-IpcPaths
$raw = Get-Content $p.stateFile -Raw
$j = $raw | ConvertFrom-Json -Depth 50
$cards = $j.cardGrid.cards
if (-not $cards) { $cards = $j.combat.cardGrid.cards }
if (-not $cards) { $cards = $j.cardGridSelection.cards }
if (-not $cards) { Write-Host "(no grid cards)"; exit }
$i = 0
foreach ($c in $cards) {
  $aff = if ($c.affliction) { $c.affliction } else { '-' }
  $upg = $c.currentUpgradeLevel
  $ench = if ($c.enchantment) { $c.enchantment } else { '-' }
  Write-Host ("[{0,2}] {1,-20} cost={2} upg={3} aff={4} ench={5}" -f $i, $c.title, $c.energyCost, $upg, $aff, $ench)
  $i++
}
