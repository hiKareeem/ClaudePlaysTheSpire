# Card-grid selection inspector. Fires for Armaments in-hand upgrade, Headbutt discard return,
# Exhume, Smith rest option, etc. Use when screen=CardGridSelection or $s.cardGrid present.
# Pair with send-cmd SelectCardsInGrid { cardIndices=[N,...] } using the [N] indices below.
. (Join-Path $PSScriptRoot '..\autopilot-lib.ps1')
$s = Read-State

if (-not $s.cardGrid -or -not $s.cardGrid.cards) {
    Write-Output "(no cardGrid on current screen: $($s.screen.name))"
    return
}

Write-Output "Screen: $($s.screen.name)"
if ($s.cardGrid.title)       { Write-Output "Title: $($s.cardGrid.title)" }
if ($s.cardGrid.description) { Write-Output "Desc:  $($s.cardGrid.description)" }
if ($null -ne $s.cardGrid.minSelection -or $null -ne $s.cardGrid.maxSelection) {
    Write-Output "Select: min=$($s.cardGrid.minSelection) max=$($s.cardGrid.maxSelection)"
}
Write-Output "--- GRID CARDS ---"
$i = 0
foreach ($entry in $s.cardGrid.cards) {
    $c = $entry.card
    if (-not $c) { Write-Output "  [$i] (null)"; $i++; continue }
    $upg = if ($c.isUpgraded) { '+' } else { '' }
    $cost = if ($null -ne $c.energyCost) { "$($c.energyCost)E" } else { '?E' }
    Write-Output ("  [{0}] {1}{2}  {3}  {4}" -f $i, $c.title, $upg, $c.rarity, $cost)
    $i++
}
