. (Join-Path $PSScriptRoot '..\autopilot-lib.ps1')
$s = Read-State
$s.restSite.options | ForEach-Object {
    $idx = [array]::IndexOf($s.restSite.options, $_)
    Write-Host "[$idx] $($_.label) - $($_.description)"
}
