param(
    [Parameter(Mandatory)][string]$Type,
    [string]$ParamsJson = '{}',
    [int]$WaitMs = 1500
)
# Dispatcher wrapper. Clears IPC, sends command, waits, prints result.
# Usage:
#   tools\send-cmd.ps1 PlayCard '{"handIndex":0,"targetIndex":0}'
#   tools\send-cmd.ps1 EndTurn
#   tools\send-cmd.ps1 SelectMapNode '{"col":2,"row":1}'
# Note: Self-target cards MUST NOT include targetIndex. Attack cards MUST include it.
. (Join-Path $PSScriptRoot '..\autopilot-lib.ps1')

Clear-Ipc
$cmd = @{ type = $Type }
if ($ParamsJson -and $ParamsJson.Trim() -ne '' -and $ParamsJson.Trim() -ne '{}') {
    try {
        $parms = $ParamsJson | ConvertFrom-Json
    } catch {
        Write-Error "Invalid JSON for -ParamsJson: $_"
        return
    }
    if ($parms) {
        $parms.PSObject.Properties | ForEach-Object { $cmd[$_.Name] = $_.Value }
    }
}

$r = Send-BridgeCommand $cmd
# Send-BridgeCommand returns { result, state, ok, stalled, id }
# where result is the parsed result.json { id, status, message }.
$status = '?'
if ($r -and $r.PSObject.Properties['result'] -and $r.result) {
    if ($r.result.PSObject.Properties['status']) { $status = $r.result.status }
    Write-Output "status=$status"
    if ($r.result.PSObject.Properties['message'] -and $r.result.message) {
        Write-Output "message=$($r.result.message)"
    }
} else {
    Write-Output "status=(no result - IPC timeout)"
}
if ($r -and $r.PSObject.Properties['ok'])      { Write-Output "ok=$($r.ok)" }
if ($r -and $r.PSObject.Properties['stalled'] -and $r.stalled) { Write-Output "stalled=true" }
Start-Sleep -Milliseconds $WaitMs
