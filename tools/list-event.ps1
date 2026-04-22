# Event option inspector. Use when screen=EventRoom.
# Pair with send-cmd SelectEventOption { optionIndex=N } where N is the index shown below.
. (Join-Path $PSScriptRoot '..\autopilot-lib.ps1')
$s = Read-State

if (-not $s.event) {
    Write-Output "(no event on current screen: $($s.screen.name))"
    return
}

Write-Output "Event: $($s.event.title)"
if ($s.event.description) { Write-Output $s.event.description }
Write-Output ""
Write-Output "--- OPTIONS ---"
if (-not $s.event.options) { Write-Output "(no options)"; return }
foreach ($opt in $s.event.options) {
    $flags = @()
    if ($opt.isLocked)        { $flags += 'LOCKED' }
    if ($opt.disableOnChosen) { $flags += 'oneshot' }
    if ($opt.wasChosen)       { $flags += 'TAKEN' }
    if ($opt.isProceed)       { $flags += 'proceed' }
    $flagStr = if ($flags.Count) { ' [' + ($flags -join ',') + ']' } else { '' }
    $title = if ($opt.title) { $opt.title } else { "(option $($opt.index))" }
    Write-Output ("  [{0}] {1}{2}" -f $opt.index, $title, $flagStr)
    if ($opt.description) { Write-Output "       $($opt.description)" }
    if ($opt.relic) { Write-Output "       relic: $($opt.relic.title)" }
}
