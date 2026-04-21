param(
    [Parameter(Mandatory)][string]$Type,
    [string]$ParamsJson = '{}',
    [int]$WaitMs = 1500
)
. (Join-Path $PSScriptRoot '..\autopilot-lib.ps1')
Clear-Ipc
$parms = $ParamsJson | ConvertFrom-Json
$cmd = @{ type = $Type }
$parms.PSObject.Properties | ForEach-Object { $cmd[$_.Name] = $_.Value }
$result = Send-BridgeCommand $cmd
Write-Output "result: $($result | ConvertTo-Json -Compress)"
Start-Sleep -Milliseconds $WaitMs
