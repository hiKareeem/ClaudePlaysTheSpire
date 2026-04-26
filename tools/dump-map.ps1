. .\autopilot-lib.ps1
$s = Read-State
$m = $s.map
Write-Host "Rows: $($m.rowCount) Cols: $($m.colCount)"
Write-Host "Current: $($m.currentCoord.col),$($m.currentCoord.row)"
Write-Host "Boss: $($m.bossCoord.col),$($m.bossCoord.row)"
Write-Host ""
Write-Host "=== AVAILABLE ==="
foreach ($a in $m.available) {
    Write-Host "  ($($a.col),$($a.row))"
}
Write-Host ""
Write-Host "=== MAP (node types) ==="
for ($r = 0; $r -lt $m.rowCount; $r++) {
    $line = "Row$($r.ToString('00')): "
    for ($c = 0; $c -lt $m.colCount; $c++) {
        $node = $m.grid[$r][$c]
        if ($node) {
            $t = $node.type
            switch ($t) {
                'Monster' { $line += '[M]' }
                'Elite' { $line += '[E]' }
                'RestSite' { $line += '[R]' }
                'Shop' { $line += '[$]' }
                'Treasure' { $line += '[T]' }
                'Event' { $line += '[?]' }
                default { $line += "[$t]" }
            }
        } else {
            $line += ' . '
        }
    }
    Write-Host $line
}
