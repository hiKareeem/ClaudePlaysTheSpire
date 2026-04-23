. (Join-Path $PSScriptRoot '..\autopilot-lib.ps1')
$p = Get-IpcPaths
$raw = Get-Content $p.stateFile -Raw
$jObj = $raw | ConvertFrom-Json -AsHashtable

function Find-Keys($obj, $path, $pattern) {
    if ($obj -is [System.Collections.IDictionary]) {
        foreach ($key in $obj.Keys) {
            if ($key -match $pattern) {
                $val = $obj[$key]
                if ($val -isnot [System.Collections.IEnumerable] -or $val -is [string]) {
                    Write-Host "$path.$key = $val"
                } else {
                    Write-Host "$path.$key = [complex]"
                }
            }
            if ($obj[$key] -is [System.Collections.IDictionary] -or $obj[$key] -is [System.Collections.IList]) {
                Find-Keys $obj[$key] "$path.$key" $pattern
            }
        }
    } elseif ($obj -is [System.Collections.IList]) {
        for ($i = 0; $i -lt $obj.Count; $i++) {
            if ($obj[$i] -is [System.Collections.IDictionary] -or $obj[$i] -is [System.Collections.IList]) {
                Find-Keys $obj[$i] "$path[$i]" $pattern
            }
        }
    }
}

Write-Host "=== ALL STAR/FORGE/MATERIAL FIELDS ==="
Find-Keys $jObj "root" "star|forge|material"
