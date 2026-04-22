$here = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $here '..\autopilot-lib.ps1')
$s = Read-State
Write-Host ("revision=" + $s.revision + " screen=" + $s.screen)
Write-Host "top-level state keys:"
$s | Get-Member -MemberType NoteProperty | ForEach-Object { Write-Host ("  " + $_.Name) }
Write-Host "---"
Write-Host "handSelect in root? $($null -ne $s.handSelect)"
if ($s.handSelect) { $s.handSelect | ConvertTo-Json -Depth 8 }
Write-Host "cardGrid in root? $($null -ne $s.cardGrid)"
if ($s.cardGrid) { $s.cardGrid | ConvertTo-Json -Depth 8 }
