# Rest site option inspector. Use when screen=RestSiteRoom.
# Pair with send-cmd SelectRestSiteOption { optionIndex=N } where N is the index shown below.
. (Join-Path $PSScriptRoot '..\autopilot-lib.ps1')
$s = Read-State

if (-not $s.restSite) {
    Write-Output "(no restSite on current screen: $($s.screen.name))"
    return
}
if (-not $s.restSite.options) { Write-Output "(restSite has no options)"; return }

Write-Output "Screen: $($s.screen.name)"
Write-Output "HP: $($s.run.currentHp) / $($s.run.maxHp)"
Write-Output "--- REST OPTIONS ---"
foreach ($opt in $s.restSite.options) {
    $flag = if ($opt.isEnabled) { '' } else { ' [DISABLED]' }
    $title = if ($opt.title) { $opt.title } else { $opt.kind }
    Write-Output ("  [{0}] {1} ({2}){3}" -f $opt.index, $title, $opt.kind, $flag)
    if ($opt.description) { Write-Output "       $($opt.description)" }
    if ($opt.extra) {
        $parts = @()
        if ($null -ne $opt.extra.healAmount -and $opt.extra.healAmount -ge 0) { $parts += "heal=$($opt.extra.healAmount)" }
        if ($null -ne $opt.extra.smithCount)                                  { $parts += "smithCount=$($opt.extra.smithCount)" }
        if ($null -ne $opt.extra.removableCardCount -and $opt.extra.removableCardCount -ge 0) { $parts += "removable=$($opt.extra.removableCardCount)" }
        if ($parts.Count) { Write-Output ("       " + ($parts -join '  ')) }
    }
}
