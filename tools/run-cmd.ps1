
param(
  [Parameter(Mandatory)][string]$CmdType,
  [string]$ParamsJson = '{}'
)
. (Join-Path $PSScriptRoot '..\autopilot-lib.ps1')
$params = $ParamsJson | ConvertFrom-Json
$ht = @{ type = $CmdType }
if ($params) {
  $params.PSObject.Properties | ForEach-Object { $ht[$_.Name] = $_.Value }
}
$r = Send-BridgeCommand $ht
$resultJson = if ($r.result) { $r.result | ConvertTo-Json -Compress } else { 'null' }
$stateJson = if ($r.state) { $r.state | ConvertTo-Json -Depth 10 -Compress } else { 'null' }
Write-Output "OK=$($r.ok)"
Write-Output "RESULT=$resultJson"
Write-Output "SCREEN=$($r.state.screen.name)"
Write-Output "REVISION=$($r.state.revision)"
if ($r.state.run) {
  Write-Output "HP=$($r.state.run.currentHp)/$($r.state.run.maxHp)"
  Write-Output "GOLD=$($r.state.run.gold)"
  Write-Output "FLOOR=$($r.state.run.totalFloor)"
}
