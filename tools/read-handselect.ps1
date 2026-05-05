. (Join-Path $PSScriptRoot '..\autopilot-lib.ps1')
$p = Get-IpcPaths
$state = Get-Content $p.StateFile -Raw | ConvertFrom-Json
$state.handSelect | ConvertTo-Json -Depth 6
