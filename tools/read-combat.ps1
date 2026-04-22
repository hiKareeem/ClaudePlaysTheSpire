# Full combat inspector. Use during combat screens.
# Pair with:
#   send-cmd PlayCard { handIndex, [targetIndex] }   (targetIndex REQUIRED for attacks, FORBIDDEN for self-target)
#   send-cmd EndTurn
#   send-cmd UsePotion { potionIndex, [targetIndex | targetSelf=true] }
. (Join-Path $PSScriptRoot '..\autopilot-lib.ps1')

function HasProp($obj, [string]$name) {
    if ($null -eq $obj) { return $false }
    return [bool]($obj.PSObject.Properties[$name])
}
function Val($obj, [string]$name, $fallback = $null) {
    if (-not (HasProp $obj $name)) { return $fallback }
    $v = $obj.$name
    if ($null -eq $v) { return $fallback }
    return $v
}

$s = Read-State
$c = $s.combat
if (-not $c) { Write-Host 'Not in combat.'; return }

$round  = Val $c 'roundNumber' '?'
$side   = Val $c 'currentSide' '?'
$energy = Val $c 'energy' '?'
$maxEn  = Val $c 'maxEnergy' '?'
Write-Host ("ROUND {0}  side={1}  energy={2}/{3}" -f $round, $side, $energy, $maxEn)

$p = $c.player
if ($p) {
    Write-Host ("PLAYER  HP:{0}/{1}  Block:{2}" -f (Val $p 'currentHp' '?'), (Val $p 'maxHp' '?'), (Val $p 'block' 0))
    $pw = Val $p 'powers' $null
    if ($pw) {
        foreach ($x in $pw) {
            $nm = Val $x 'title' (Val $x 'id' '?')
            $amt = Val $x 'amount' ''
            Write-Host ("  Power: {0} x{1}" -f $nm, $amt)
            $desc = Val $x 'description' ''
            if ($desc) { Write-Host ("    {0}" -f $desc) }
        }
    }
}

# HAND
$cards = $null
if (HasProp $c 'hand') {
    $h = $c.hand
    if ($h -and (HasProp $h 'cards')) { $cards = $h.cards }
    elseif ($h -is [array]) { $cards = $h }
}
if ($cards) {
    Write-Host ""
    Write-Host ("HAND ({0}):" -f $cards.Count)
    for ($i = 0; $i -lt $cards.Count; $i++) {
        $k = $cards[$i]
        $flags = @()
        if (Val $k 'isPlayable' $false) { $flags += 'play' } else { $flags += 'NOPLAY' }
        $tt = Val $k 'targetType' ''
        if ($tt) { $flags += "tgt=$tt" }
        $cost = Val $k 'effectiveEnergyCost' (Val $k 'energyCost' '?')
        Write-Host ("  [{0}] {1}  E:{2}  {3}" -f $i, (Val $k 'title' '?'), $cost, ($flags -join ' '))
        $desc = Val $k 'description' ''
        if ($desc) { Write-Host ("       {0}" -f $desc) }
    }
}

# ENEMIES
$enemies = Val $c 'enemies' $null
if ($enemies) {
    Write-Host ""
    Write-Host ("ENEMIES ({0}):" -f $enemies.Count)
    for ($i = 0; $i -lt $enemies.Count; $i++) {
        $e = $enemies[$i]
        if (-not $e) { Write-Host "  [$i] (null)"; continue }
        $nm = Val $e 'name' (Val $e 'title' '?')
        Write-Host ("  [{0}] {1}  HP:{2}/{3}  Block:{4}" -f $i, $nm, (Val $e 'currentHp' '?'), (Val $e 'maxHp' '?'), (Val $e 'block' 0))
        $eps = Val $e 'powers' $null
        if ($eps) {
            foreach ($x in $eps) {
                $pn = Val $x 'title' (Val $x 'id' '?')
                Write-Host ("      Power: {0} x{1}" -f $pn, (Val $x 'amount' ''))
                $pd = Val $x 'description' ''
                if ($pd) { Write-Host ("        {0}" -f $pd) }
            }
        }
        $its = Val $e 'intents' $null
        if ($its) {
            foreach ($it in $its) {
                $line = "      Intent: " + (Val $it 'intentType' '?')
                $lbl = Val $it 'label' (Val $it 'title' '')
                if ($lbl) { $line += " - $lbl" }
                $dmg = Val $it 'damage' 0
                if ($dmg -and $dmg -gt 0) {
                    $line += " [dmg $dmg"
                    $rep = Val $it 'repeats' 1
                    if ($rep -and $rep -gt 1) { $line += " x$rep" }
                    $line += "]"
                }
                Write-Host $line
                $id = Val $it 'description' ''
                if ($id) { Write-Host ("        {0}" -f $id) }
            }
        }
    }
}

# ALLIES
$allies = Val $c 'allies' $null
if ($allies -and $allies.Count -gt 0) {
    Write-Host ""
    Write-Host ("ALLIES ({0}):" -f $allies.Count)
    for ($i = 0; $i -lt $allies.Count; $i++) {
        $a = $allies[$i]
        if (-not $a) { continue }
        Write-Host ("  [{0}] {1}  HP:{2}/{3}  Block:{4}" -f $i, (Val $a 'name' '?'), (Val $a 'currentHp' '?'), (Val $a 'maxHp' '?'), (Val $a 'block' 0))
    }
}

# Pile counts
function PileCount($pile) {
    if (-not $pile) { return 0 }
    if (HasProp $pile 'cards' -and $pile.cards) { return $pile.cards.Count }
    if ($pile -is [array]) { return $pile.Count }
    return 0
}
$dc = PileCount (Val $c 'drawPile' $null)
$di = PileCount (Val $c 'discardPile' $null)
$ex = PileCount (Val $c 'exhaustPile' $null)
Write-Host ""
Write-Host ("Piles  Draw:{0}  Discard:{1}  Exhaust:{2}" -f $dc, $di, $ex)
