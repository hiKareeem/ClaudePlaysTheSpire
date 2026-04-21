. (Join-Path $PSScriptRoot '..\autopilot-lib.ps1')
$s = Read-State
$s.cardGrid.cards | ForEach-Object {
    $idx = [array]::IndexOf($s.cardGrid.cards, $_)
    $upg = if ($_.card.isUpgraded) { "+" } else { "" }
    Write-Host "[$idx] $($_.card.title)$upg ($($_.card.rarity)) $($_.card.energyCost)E"
}
