. (Join-Path $PSScriptRoot '..\autopilot-lib.ps1')
$s = Read-State
$s.cardRewardOptions.cards | ForEach-Object {
    Write-Host "$($_.card.title) ($($_.card.rarity)) - $($_.card.energyCost)E - $($_.card.description)"
}
