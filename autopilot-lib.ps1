# autopilot-lib.ps1 — HermesBridge-StS2 IPC helper library
#
# This is a LIBRARY, not a runner. It defines primitives for an agent
# (human or LLM) to drive the bridge tick-by-tick. It contains NO
# decision logic, NO loops, and NO screen dispatch.
#
# Dot-source it, then drive the game yourself:
#
#   . .\autopilot-lib.ps1
#   $s = Read-State
#   $r = Send-BridgeCommand @{ type='StartRun'; character='NECROBINDER' }
#   $s = $r.state
#   # ...decide...
#   $r = Send-BridgeCommand @{ type='PlayCard'; handIndex=0 }
#   ...
#
# DRIVER GOTCHA: pwsh -Command "..." silently strips bare `$` characters
# from its argument, which mangles state references like $s.combat.energy
# into .combat.energy. If you (or your agent) need to interpolate state
# variables across calls, use a script file (`pwsh -File foo.ps1` or
# `pwsh -c ". .\autopilot-lib.ps1; ..."` with the body wrapped in a
# string literal) rather than building a `-Command` argument with
# variables. This has bitten multiple drivers.
#   Write-SessionLog -Character 'NECROBINDER' -HaltReason 'DEATH floor=11' -FinalState $r.state
#
# See SKILL.md at repo root for the full spec.

Set-StrictMode -Version Latest

# ---------------- Paths ----------------
$script:IpcDir      = Join-Path $env:APPDATA 'SlayTheSpire2\hermesbridge'
$script:StateFile   = Join-Path $script:IpcDir 'state.json'
$script:CmdsFile    = Join-Path $script:IpcDir 'commands.json'
$script:ResFile     = Join-Path $script:IpcDir 'result.json'
$script:TraceFile   = Join-Path $script:IpcDir 'trace.log'
$script:OverlayFile = Join-Path $script:IpcDir 'overlay.txt'
$script:OverlayLog  = Join-Path $script:IpcDir 'overlay.log'

$script:RepoRoot = $PSScriptRoot
$script:LogDir   = Join-Path $script:RepoRoot 'docs'

# ---------------- Session state ----------------
$script:SessionFile = Join-Path $script:IpcDir 'autopilot-session.json'
$script:NextId   = 1
$script:CmdCount = 0
$script:Findings = New-Object System.Collections.Generic.List[string]
$script:RunStart = Get-Date

if (Test-Path $script:SessionFile) {
    try {
        $sess = Get-Content $script:SessionFile -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($sess.nextId -and $sess.nextId -gt 1) {
            $script:NextId = [Math]::Max($script:NextId, $sess.nextId)
        }
        if ($sess.cmdCount) {
            $script:CmdCount = $sess.cmdCount
        }
    } catch { }
}

function Save-SessionState {
    $sess = @{ nextId = $script:NextId; cmdCount = $script:CmdCount }
    $sess | ConvertTo-Json | Set-Content $script:SessionFile -Encoding UTF8
}

function Reset-Session {
    <#
    .SYNOPSIS
    Reset per-run session counters. Call at the start of a fresh run.
    Optionally pass -StartingId if you're resuming mid-session (e.g.
    after a manual command run, to keep ids monotonic).
    #>
    param([int]$StartingId = 1)
    $script:NextId   = [Math]::Max($StartingId, $script:NextId)
    $script:CmdCount = 0
    $script:Findings = New-Object System.Collections.Generic.List[string]
    $script:RunStart = Get-Date
    Save-SessionState
}

function Get-IpcPaths {
    [pscustomobject]@{
        IpcDir      = $script:IpcDir
        StateFile   = $script:StateFile
        CmdsFile    = $script:CmdsFile
        ResFile     = $script:ResFile
        TraceFile   = $script:TraceFile
        OverlayFile = $script:OverlayFile
        OverlayLog  = $script:OverlayLog
    }
}

# ---------------- Diagnostics ----------------
function Log-Finding {
    <#
    .SYNOPSIS
    Record a stability finding for later inclusion in the session log.
    Use freely from caller code.
    #>
    param([Parameter(Mandatory)][string]$Message)
    $ts = (Get-Date).ToString('HH:mm:ss')
    $line = "[$ts] $Message"
    Write-Host "  ! $line" -ForegroundColor Yellow
    $script:Findings.Add($line) | Out-Null
}

function Get-Findings { ,@($script:Findings) }

# ---------------- State I/O ----------------
function Read-State {
    <#
    .SYNOPSIS
    Read state.json with retry (the bridge may be mid-write).
    Returns the parsed object. Throws on persistent failure.
    #>
    for ($i=0; $i -lt 20; $i++) {
        try { return Get-Content $script:StateFile -Raw -EA Stop | ConvertFrom-Json } catch { Start-Sleep -Milliseconds 50 }
    }
    throw "Could not read $script:StateFile"
}

function Wait-Revision {
    <#
    .SYNOPSIS
    Block until state.json revision exceeds $AfterRevision, or timeout.
    Returns the fresh state object, or $null on timeout.
    #>
    param(
        [Parameter(Mandatory)][int]$AfterRevision,
        [int]$TimeoutSec = 30
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        try {
            $s = Read-State
            if ([int]$s.revision -gt $AfterRevision) { return $s }
        } catch { }
        Start-Sleep -Milliseconds 150
    }
    return $null
}

function Clear-Ipc {
    <#
    .SYNOPSIS
    Delete stale commands.json and result.json. Call once at session
    start so a stale command from a previous crash doesn't double-apply
    on game boot.
    #>
    foreach ($f in @($script:CmdsFile, $script:ResFile)) {
        if (Test-Path $f) { Remove-Item $f -Force -EA SilentlyContinue }
    }
}

# ---------------- Command dispatch ----------------
function Send-BridgeCommand {
    <#
    .SYNOPSIS
    Write a single command, wait for its result, wait for a state
    revision bump, return both.
    .OUTPUTS
    pscustomobject with fields:
      result   — the parsed result.json (or $null on IPC timeout)
      state    — the state object after the command (always populated if readable)
      ok       — bool: result.status == 'ok' AND no stall
      stalled  — bool: revision did not advance within RevisionTimeoutSec
      id       — the command id used
    Does not throw on bridge-level errors; the caller inspects .ok.
    .NOTES
    Automatically logs IPC_TIMEOUT, CMD_ERROR, STALL findings via Log-Finding.
    #>
    param(
        [Parameter(Mandatory)][hashtable]$Command,
        [int]$ResultTimeoutSec = 10,
        [int]$RevisionTimeoutSec = 30
    )

    $script:CmdCount++
    $id = $script:NextId; $script:NextId++
    Save-SessionState

    $preState = $null
    try { $preState = Read-State } catch { }
    $preRev = if ($preState) { [int]$preState.revision } else { -1 }

    if (Test-Path $script:ResFile) { Remove-Item $script:ResFile -Force -EA SilentlyContinue }

    $payload = @{ id = $id; command = $Command } | ConvertTo-Json -Depth 10 -Compress
    Set-Content -Path $script:CmdsFile -Value $payload -Encoding UTF8 -NoNewline

    # Wait for matching result.json
    $deadline = (Get-Date).AddSeconds($ResultTimeoutSec)
    $result = $null
    while ((Get-Date) -lt $deadline) {
        if (Test-Path $script:ResFile) {
            try {
                $r = Get-Content $script:ResFile -Raw -EA Stop | ConvertFrom-Json
                if ([int]$r.id -eq $id) { $result = $r; break }
            } catch { }
        }
        Start-Sleep -Milliseconds 100
    }

    if (-not $result) {
        Log-Finding "IPC_TIMEOUT id=$id cmd=$($Command.type) (no result after ${ResultTimeoutSec}s)"
        $s = $null; try { $s = Read-State } catch { }
        return [pscustomobject]@{ result=$null; state=$s; ok=$false; stalled=$false; id=$id }
    }

    if ($result.status -ne 'ok') {
        Log-Finding "CMD_ERROR id=$id cmd=$($Command.type) msg='$($result.message)'"
    }

    $state = Wait-Revision -AfterRevision $preRev -TimeoutSec $RevisionTimeoutSec
    $stalled = $false
    if (-not $state) {
        $stalled = $true
        try { $state = Read-State } catch { }
        $scr = if ($state -and $state.screen) { $state.screen.name } else { '?' }
        Log-Finding "STALL id=$id cmd=$($Command.type) screen=$scr preRev=$preRev (no revision bump in ${RevisionTimeoutSec}s)"
    }

    return [pscustomobject]@{
        result  = $result
        state   = $state
        ok      = (($result.status -eq 'ok') -and (-not $stalled))
        stalled = $stalled
        id      = $id
    }
}

# ---------------- Session log ----------------
function Write-SessionLog {
    <#
    .SYNOPSIS
    Append a per-run markdown section to docs/autopilot-session-<date>.md.
    Call once per run, after halt.
    #>
    param(
        [Parameter(Mandatory)][string]$Character,
        [Parameter(Mandatory)][string]$HaltReason,
        [Parameter(Mandatory)][object]$FinalState,
        [int]$TraceTailLines = 100
    )

    if (-not (Test-Path $script:LogDir)) { New-Item -ItemType Directory -Path $script:LogDir | Out-Null }
    $logFile = Join-Path $script:LogDir ("autopilot-session-{0}.md" -f (Get-Date -Format 'yyyy-MM-dd'))

    $startUtc = $script:RunStart.ToUniversalTime().ToString('yyyy-MM-ddTHH:mmZ')
    $floor = if ($FinalState -and $FinalState.run) { [int]$FinalState.run.totalFloor } else { -1 }
    $hp = if ($FinalState -and $FinalState.run) { "$($FinalState.run.currentHp)/$($FinalState.run.maxHp)" } else { 'n/a' }
    $lastScr = if ($FinalState -and $FinalState.screen) { $FinalState.screen.name } else { '?' }
    $dur = ((Get-Date) - $script:RunStart).TotalMinutes

    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine("## Run $startUtc - $Character")
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("- Halt reason: $HaltReason")
    [void]$sb.AppendLine("- End floor: $floor, hp $hp, last screen $lastScr")
    [void]$sb.AppendLine(("- Duration: {0:N1} min, {1} commands, last id {2}" -f $dur, $script:CmdCount, ($script:NextId - 1)))
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("### Stability findings")
    if ($script:Findings.Count -eq 0) {
        [void]$sb.AppendLine("- None.")
    } else {
        foreach ($f in $script:Findings) { [void]$sb.AppendLine("- $f") }
    }
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("### trace.log tail (last $TraceTailLines lines)")
    [void]$sb.AppendLine('```')
    if (Test-Path $script:TraceFile) {
        Get-Content $script:TraceFile -Tail $TraceTailLines | ForEach-Object { [void]$sb.AppendLine($_) }
    }
    [void]$sb.AppendLine('```')
    [void]$sb.AppendLine()

    Add-Content -Path $logFile -Value $sb.ToString() -Encoding UTF8
    Write-Host "Session log written: $logFile" -ForegroundColor Green
    return $logFile
}

# ---------------- Overlay (for OBS / post-hoc SRT) ----------------
# overlay.txt = single line, overwritten each call. Point an OBS "Text (GDI+)"
#   source at it with "Read from file" enabled to render live subtitles.
# overlay.log = append-only, tab-separated, ISO timestamp + line. Feed to
#   New-OverlaySrt after a session to produce an .srt for post-production.

function Set-OverlayText {
    <#
    .SYNOPSIS
    Write a one-line action justification for the stream overlay.
    Appends to overlay.log with a UTC ISO timestamp for SRT generation.
    .PARAMETER Text
    The justification. Keep under ~120 chars — longer lines wrap or clip.
    .PARAMETER Prefix
    Optional unicode glyph. Suggested: spades for PlayCard, diamonds
    for rewards, clubs for map, hearts for potions, flag for combat end.
    #>
    param(
        [Parameter(Mandatory)][string]$Text,
        [string]$Prefix = ''
    )
    $line = if ($Prefix) { "$Prefix $Text" } else { $Text }
    $isoTs = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss.fffZ')
    Set-Content -Path $script:OverlayFile -Value $line -Encoding UTF8 -NoNewline
    Add-Content -Path $script:OverlayLog -Value "$isoTs`t$line" -Encoding UTF8
}

function Clear-Overlay {
    <#
    .SYNOPSIS
    Blank the live overlay file. Does not touch overlay.log.
    #>
    Set-Content -Path $script:OverlayFile -Value '' -Encoding UTF8 -NoNewline
}

function New-OverlaySrt {
    <#
    .SYNOPSIS
    Convert overlay.log to a .srt subtitle file aligned to a recording.
    .DESCRIPTION
    overlay.log contains UTC timestamps plus one-line justifications.
    Pass -RecordingStartUtc matching the start of your OBS recording;
    each overlay line becomes a cue, ending at the next line's timestamp
    (or +MaxCueSec if it is the last line).
    .PARAMETER RecordingStartUtc
    The UTC DateTime your OBS recording began. Anything before this is
    discarded; anything after is offset relative to it.
    .PARAMETER OutputPath
    Where to write the .srt. Default: overlay-<yyyyMMdd-HHmmss>.srt next
    to overlay.log.
    .PARAMETER MinCueSec
    Minimum cue duration floor. Default 1.5s so quick back-to-back plays
    don't flash unreadably.
    .PARAMETER MaxCueSec
    Maximum cue duration ceiling. Default 6s so the last line of a combat
    doesn't linger through the next map.
    #>
    param(
        [Parameter(Mandatory)][datetime]$RecordingStartUtc,
        [string]$OutputPath,
        [double]$MinCueSec = 1.5,
        [double]$MaxCueSec = 6.0
    )

    if (-not (Test-Path $script:OverlayLog)) {
        throw "No overlay.log at $script:OverlayLog"
    }

    if (-not $OutputPath) {
        $stamp = (Get-Date -Format 'yyyyMMdd-HHmmss')
        $OutputPath = Join-Path (Split-Path $script:OverlayLog -Parent) "overlay-$stamp.srt"
    }

    $startUtc = $RecordingStartUtc.ToUniversalTime()
    $lines = Get-Content $script:OverlayLog -Encoding UTF8 | Where-Object { $_ -match "`t" }

    $cues = @()
    foreach ($l in $lines) {
        $parts = $l -split "`t", 2
        $ts = [datetime]::Parse($parts[0]).ToUniversalTime()
        if ($ts -lt $startUtc) { continue }
        $cues += [pscustomobject]@{
            OffsetSec = ($ts - $startUtc).TotalSeconds
            Text      = $parts[1]
        }
    }

    if ($cues.Count -eq 0) {
        Write-Warning "No overlay.log entries after $RecordingStartUtc — nothing to write."
        return
    }

    function _fmt([double]$sec) {
        if ($sec -lt 0) { $sec = 0 }
        $ts = [timespan]::FromSeconds($sec)
        '{0:00}:{1:00}:{2:00},{3:000}' -f $ts.Hours, $ts.Minutes, $ts.Seconds, $ts.Milliseconds
    }

    $sb = New-Object System.Text.StringBuilder
    for ($i = 0; $i -lt $cues.Count; $i++) {
        $start = $cues[$i].OffsetSec
        $rawEnd = if ($i -lt $cues.Count - 1) { $cues[$i+1].OffsetSec } else { $start + $MaxCueSec }
        $dur = [Math]::Min([Math]::Max($rawEnd - $start, $MinCueSec), $MaxCueSec)
        $end = $start + $dur
        [void]$sb.AppendLine(($i + 1))
        [void]$sb.AppendLine((_fmt $start) + ' --> ' + (_fmt $end))
        [void]$sb.AppendLine($cues[$i].Text)
        [void]$sb.AppendLine()
    }

    Set-Content -Path $OutputPath -Value $sb.ToString() -Encoding UTF8
    Write-Host "Wrote $($cues.Count) cues to $OutputPath" -ForegroundColor Green
    return $OutputPath
}

Write-Host "autopilot-lib loaded. Primitives available:" -ForegroundColor Green
Write-Host "  Read-State, Wait-Revision, Send-BridgeCommand, Clear-Ipc" -ForegroundColor Gray
Write-Host "  Log-Finding, Get-Findings, Write-SessionLog, Reset-Session" -ForegroundColor Gray
Write-Host "  Set-OverlayText, Clear-Overlay, New-OverlaySrt" -ForegroundColor Gray
Write-Host "  Get-IpcPaths" -ForegroundColor Gray
