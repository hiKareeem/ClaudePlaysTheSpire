$gameDir = 'E:\SteamLibrary\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64'
Push-Location $gameDir
try {
    try { Add-Type -Path "$gameDir\GodotSharp.dll" -ErrorAction SilentlyContinue } catch {}
    try { Add-Type -Path "$gameDir\BaseLib.dll" -ErrorAction SilentlyContinue } catch {}
    $asm = [System.Reflection.Assembly]::LoadFrom("$gameDir\sts2.dll")
    try { $types = $asm.GetTypes() } catch [System.Reflection.ReflectionTypeLoadException] { $types = $_.Exception.Types | Where-Object { $_ -ne $null } }
    $names = @('TreasureRoom','TreasureRelicPicking','NTreasureRoom','NTreasureButton','NTreasureRoomRelicCollection','NTreasureRoomRelicHolder','TreasureRoomHandler','NetPickRelicAction','TreasureChestOpenedMessage')
    foreach ($n in $names) {
        $t = $types | Where-Object { $_ -and $_.Name -eq $n } | Select-Object -First 1
        if (-not $t) { Write-Host "MISSING $n"; continue }
        Write-Host "=== $($t.FullName) ==="
        $flags = [System.Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly'
        try { $t.GetFields($flags) | ForEach-Object { "F $($_.FieldType.Name) $($_.Name)" } } catch {}
        try { $t.GetProperties($flags) | ForEach-Object { "P $($_.PropertyType.Name) $($_.Name)" } } catch {}
        try { $t.GetMethods($flags) | ForEach-Object {
            $ps = ($_.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ', '
            "M $($_.ReturnType.Name) $($_.Name)($ps)"
        } } catch {}
    }
} finally {
    Pop-Location
}
