. (Join-Path $PSScriptRoot '..\autopilot-lib.ps1')
$state = Read-State
if ($state.treasure) {
    Write-Host "=== TREASURE ==="
    $state.treasure | ConvertTo-Json -Depth 5
} else {
    Write-Host "No treasure key in state."
    Write-Host "Keys: $($state.PSObject.Properties.Name -join ', ')"
}
