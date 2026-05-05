. (Join-Path $PSScriptRoot '..\autopilot-lib.ps1')
$paths = Get-IpcPaths
$j = Get-Content -Raw $paths.StateFile | ConvertFrom-Json
"map.bossName=$($j.map.bossName)"
"map.bossId=$($j.map.bossId)"
"map.boss=$($j.map.boss)"
"run.bossName=$($j.run.bossName)"
"run.bossId=$($j.run.bossId)"
$j.map | ConvertTo-Json -Depth 4 -Compress | Out-String | ForEach-Object { if ($_.Length -gt 4000) { $_.Substring(0,4000) } else { $_ } }
