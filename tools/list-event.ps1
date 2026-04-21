. (Join-Path $PSScriptRoot '..\autopilot-lib.ps1')
$s = Read-State
$s.event.options | ForEach-Object {
    $idx = [array]::IndexOf($s.event.options, $_)
    Write-Host "[$idx]"
    $_.PSObject.Properties | ForEach-Object { Write-Host "  $($_.Name) = $($_.Value)" }
}
