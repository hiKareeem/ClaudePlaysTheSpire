. (Join-Path $PSScriptRoot '..\autopilot-lib.ps1')
$p = Get-IpcPaths
$state = Get-Content $p.StateFile -Raw | ConvertFrom-Json
$cards = $state.chooseACardScreen.cards
foreach ($c in $cards) {
  Write-Host ("[{0}] {1} ({2}E) - {3}" -f $c.index, $c.card.title, $c.card.energyCost, $c.card.description)
}
