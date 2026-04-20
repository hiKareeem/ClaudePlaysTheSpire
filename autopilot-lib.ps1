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
#   Write-SessionLog -Character 'NECROBINDER' -HaltReason 'DEATH floor=11' -FinalState $r.state
#
# See SKILL.md at repo root for the full spec.

Set-StrictMode -Version Latest

# ---------------- Paths ----------------
$script:IpcDir    = Join-Path $env:APPDATA 'SlayTheSpire2\hermesbridge'
$script:StateFile = Join-Path $script:IpcDir 'state.json'
$script:CmdsFile  = Join-Path $script:IpcDir 'commands.json'
$script:ResFile   = Join-Path $script:IpcDir 'result.json'
$script:TraceFile = Join-Path $script:IpcDir 'trace.log'

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
        IpcDir    = $script:IpcDir
        StateFile = $script:StateFile
        CmdsFile  = $script:CmdsFile
        ResFile   = $script:ResFile
        TraceFile = $script:TraceFile
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

Write-Host "autopilot-lib loaded. Primitives available:" -ForegroundColor Green
Write-Host "  Read-State, Wait-Revision, Send-BridgeCommand, Clear-Ipc" -ForegroundColor Gray
Write-Host "  Log-Finding, Get-Findings, Write-SessionLog, Reset-Session" -ForegroundColor Gray
Write-Host "  Get-IpcPaths" -ForegroundColor Gray
