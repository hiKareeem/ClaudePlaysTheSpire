. (Join-Path $PSScriptRoot '..\autopilot-lib.ps1')
$s = Read-State
$m = $s.map
if (-not $m) { Write-Host 'No active map.'; return }

Write-Host ("MAP  {0}x{1}" -f $m.rowCount, $m.colCount)
if ($m.currentCoord) {
    Write-Host ("Current: col={0} row={1}" -f $m.currentCoord.col, $m.currentCoord.row)
} else {
    Write-Host 'Current: <not yet on map>'
}
if ($m.bossCoord) {
    Write-Host ("Boss:    col={0} row={1}" -f $m.bossCoord.col, $m.bossCoord.row)
}

# Available next nodes — what SelectMapNode can target RIGHT NOW
if ($m.available) {
    Write-Host ""
    Write-Host ("AVAILABLE NEXT ({0}):" -f $m.available.Count)
    foreach ($n in $m.available) {
        if ($n.PSObject.Properties['error'] -and $n.error) { Write-Host ("  ERROR: {0}" -f $n.error); continue }
        Write-Host ("  col={0} row={1}  {2}  state={3}" -f $n.col, $n.row, $n.pointType, $n.state)
    }
} else {
    Write-Host 'No travelable nodes (not on map screen?).'
}

# Build a quick visited lookup
$visitedKeys = @{}
if ($m.visited) {
    foreach ($v in $m.visited) { $visitedKeys["$($v.col),$($v.row)"] = $true }
}
$availableKeys = @{}
if ($m.available) {
    foreach ($a in $m.available) { $availableKeys["$($a.col),$($a.row)"] = $true }
}

# Full grid grouped by row (bottom-up == path forward)
if ($m.grid) {
    $byRow = $m.grid | Group-Object -Property row | Sort-Object { [int]$_.Name }
    Write-Host ""
    Write-Host "GRID (*=current  +=available  .=visited  col[type] -> children):"
    foreach ($g in $byRow) {
        $cells = ($g.Group | Sort-Object { [int]$_.col } | ForEach-Object {
            $key = "$($_.col),$($_.row)"
            $kids = ''
            if ($_.children -and $_.children.Count -gt 0) {
                $kids = ' -> ' + (($_.children | ForEach-Object { "$($_.col),$($_.row)" }) -join ' | ')
            }
            $mark = ' '
            if ($m.currentCoord -and $_.col -eq $m.currentCoord.col -and $_.row -eq $m.currentCoord.row) { $mark = '*' }
            elseif ($availableKeys.ContainsKey($key)) { $mark = '+' }
            elseif ($visitedKeys.ContainsKey($key))   { $mark = '.' }
            "{0}{1}[{2}]{3}" -f $mark, $_.col, $_.pointType, $kids
        }) -join '   '
        Write-Host ("  r{0}: {1}" -f $g.Name, $cells)
    }
}

if ($m.visited) {
    Write-Host ""
    Write-Host ("Visited: {0} nodes" -f $m.visited.Count)
}
