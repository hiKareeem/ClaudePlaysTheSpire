. (Join-Path $PSScriptRoot '..\autopilot-lib.ps1')
$s = Read-State
if ($s.shop) {
    $s.shop | ConvertTo-Json -Depth 8
} else {
    Write-Output "No shop data in state"
}
