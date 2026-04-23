. (Join-Path $PSScriptRoot '..\autopilot-lib.ps1')
$p = Get-IpcPaths
$raw = Get-Content $p.stateFile -Raw
$jObj = $raw | ConvertFrom-Json -AsHashtable

function Show-Obj($obj, $depth, $maxDepth) {
    if ($depth -gt $maxDepth) { Write-Host ("  " * $depth + "[max depth]"); return }
    if ($obj -is [System.Collections.IDictionary]) {
        foreach ($key in $obj.Keys) {
            $val = $obj[$key]
            if ($null -eq $val) {
                Write-Host ("  " * $depth + "$key = null")
            } elseif ($val -is [string] -or $val -is [int] -or $val -is [long] -or $val -is [double] -or $val -is [bool]) {
                Write-Host ("  " * $depth + "$key = $val")
            } elseif ($val -is [System.Collections.IList]) {
                Write-Host ("  " * $depth + "$key = [list $($val.Count) items]")
                if ($val.Count -gt 0 -and $val[0] -is [System.Collections.IDictionary]) {
                    for ($i = 0; $i -lt [Math]::Min($val.Count, 3); $i++) {
                        Write-Host ("  " * ($depth+1) + "[$i]:")
                        Show-Obj $val[$i] ($depth+2) $maxDepth
                    }
                }
            } elseif ($val -is [System.Collections.IDictionary]) {
                Write-Host ("  " * $depth + "$key = [hashtable]")
                Show-Obj $val ($depth+1) $maxDepth
            } else {
                Write-Host ("  " * $depth + "$key = [$($val.GetType().Name)]")
            }
        }
    }
}

# Check chooseACardScreen
if ($jObj.ContainsKey('chooseACardScreen') -and $jObj.chooseACardScreen) {
    Write-Host "=== chooseACardScreen ==="
    Show-Obj $jObj.chooseACardScreen 0 3
}

# Check combat.cardGrid
if ($jObj.combat -and $jObj.combat.ContainsKey('cardGrid') -and $jObj.combat.cardGrid) {
    Write-Host "=== combat.cardGrid ==="
    Show-Obj $jObj.combat.cardGrid 0 3
}
