<#
.SYNOPSIS
    Operator-side preflight for a slot before starting a SpireBench run.

.DESCRIPTION
    Composes the existing tools/preflight-dll-version.ps1 (DLL version,
    bridge floor, IPC-root match) with operator-only checks that reduce
    "wasted run" risk:

      4. Per-slot IPC env-var present (SPIREBRIDGE_IPC_DIR_<slot>)
      5. IPC dir is clean (no stale state.json / commands.json / *.lock)
      6. Trace.log is rotated or short (>50 MB stale trace = drop frame
         risk + slow tail; warn at 25 MB)
      7. Free disk space on the IPC dir's drive >= 2 GB
      8. StS2 process status (warn if running, since launcher prefers a
         fresh start)
      9. OpenCode CLI present on PATH (so the operator can session)

    All checks emit PASS / WARN / FAIL. Exit 1 on any FAIL, 0 otherwise.
    WARN is non-blocking; the operator's call.

.PARAMETER Slot
    Slot label (single uppercase letter: A, B, C, ...). Determines which
    SPIREBRIDGE_IPC_DIR_<slot> env var is checked.

.PARAMETER SkipDllPreflight
    Skip the existing tools/preflight-dll-version.ps1 invocation.
    For test only; default off.

.PARAMETER MinFreeGB
    Minimum free disk space in GB. Default 2.

.PARAMETER TraceWarnMB
    Trace.log size that triggers WARN. Default 25.

.PARAMETER TraceFailMB
    Trace.log size that triggers FAIL. Default 50.

.EXAMPLE
    .\tools\operator\preflight-slot.ps1 -Slot A
    .\tools\operator\preflight-slot.ps1 -Slot B -MinFreeGB 5

.NOTES
    Operator-side only. Excluded from public release zip.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Z]$')]
    [string]$Slot,

    [switch]$SkipDllPreflight,
    [int]$MinFreeGB = 2,
    [int]$TraceWarnMB = 25,
    [int]$TraceFailMB = 50
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$dllPreflight = Join-Path $repoRoot 'tools\preflight-dll-version.ps1'

# Local result accumulator. We DON'T defer to the dll preflight's exit code
# alone -- we still want to report on the slot-specific checks even after
# a dll-preflight FAIL.
$results = @()

function Add-Result {
    param([string]$Check, [string]$Status, [string]$Detail)
    $script:results += [pscustomobject]@{
        Check  = $Check
        Status = $Status
        Detail = $Detail
    }
}

# --- Step 1: bridge-side preflight (delegates to existing script) ----------
$bridgePreflightFailed = $false
if (-not $SkipDllPreflight) {
    if (-not (Test-Path $dllPreflight)) {
        Add-Result 'bridge-preflight' 'FAIL' "missing $dllPreflight"
        $bridgePreflightFailed = $true
    }
    else {
        Write-Host "[1/9] running bridge preflight..." -ForegroundColor Cyan
        # Resolve expected IPC root from the slot env var BEFORE invoking
        # so the dll preflight sees the operator-intended root.
        $slotEnvName = "SPIREBRIDGE_IPC_DIR_$Slot"
        $expected = [System.Environment]::GetEnvironmentVariable($slotEnvName)
        $args = @{}
        if ($expected) { $args['ExpectedIpcRoot'] = $expected }
        & $dllPreflight @args
        if ($LASTEXITCODE -ne 0) {
            Add-Result 'bridge-preflight' 'FAIL' "tools/preflight-dll-version.ps1 exited $LASTEXITCODE"
            $bridgePreflightFailed = $true
        }
        else {
            Add-Result 'bridge-preflight' 'PASS' 'dll-version, bridge-floor, ipc-root checks ok'
        }
    }
}
else {
    Add-Result 'bridge-preflight' 'WARN' 'skipped (-SkipDllPreflight)'
}

# --- Step 2: per-slot IPC env var ------------------------------------------
$slotEnvName = "SPIREBRIDGE_IPC_DIR_$Slot"
$slotIpcDir = [System.Environment]::GetEnvironmentVariable($slotEnvName)
if (-not $slotIpcDir) {
    Add-Result "env:$slotEnvName" 'FAIL' "not set; required for multi-instance routing"
}
else {
    Add-Result "env:$slotEnvName" 'PASS' $slotIpcDir
}

# --- Step 3: IPC dir clean -------------------------------------------------
if ($slotIpcDir -and (Test-Path $slotIpcDir)) {
    $stale = @()
    foreach ($f in @('state.json', 'commands.json', 'state.json.lock', 'commands.json.lock')) {
        $p = Join-Path $slotIpcDir $f
        if (Test-Path $p) {
            $age = (Get-Date) - (Get-Item $p).LastWriteTime
            $stale += "$f (age $([int]$age.TotalMinutes)m)"
        }
    }
    if ($stale.Count -gt 0) {
        Add-Result 'ipc-clean' 'WARN' ("stale: " + ($stale -join ', '))
    }
    else {
        Add-Result 'ipc-clean' 'PASS' "no stale state/commands/lock files"
    }
}
elseif ($slotIpcDir) {
    Add-Result 'ipc-clean' 'WARN' "ipc dir does not yet exist (will be created on bridge load)"
}
else {
    Add-Result 'ipc-clean' 'WARN' "skipped (no slot env)"
}

# --- Step 4: trace.log size ------------------------------------------------
# trace.log lives in the appdata logging dir, NOT the per-slot ipc dir.
# Path is currently fixed in the bridge: %APPDATA%\SlayTheSpire2\hermesbridge\trace.log
$traceLog = Join-Path $env:APPDATA 'SlayTheSpire2\hermesbridge\trace.log'
if (Test-Path $traceLog) {
    $sizeMB = [math]::Round(((Get-Item $traceLog).Length / 1MB), 1)
    if ($sizeMB -ge $TraceFailMB) {
        Add-Result 'trace.log' 'FAIL' "$sizeMB MB >= $TraceFailMB MB; rotate before run"
    }
    elseif ($sizeMB -ge $TraceWarnMB) {
        Add-Result 'trace.log' 'WARN' "$sizeMB MB >= $TraceWarnMB MB; consider rotation"
    }
    else {
        Add-Result 'trace.log' 'PASS' "$sizeMB MB"
    }
}
else {
    Add-Result 'trace.log' 'WARN' "$traceLog not found (bridge will create on load)"
}

# --- Step 5: free disk on the IPC drive ------------------------------------
if ($slotIpcDir) {
    try {
        $driveLetter = (Split-Path -Qualifier (Resolve-Path $slotIpcDir -ErrorAction SilentlyContinue)).TrimEnd(':')
        if ($driveLetter) {
            $drive = Get-PSDrive -Name $driveLetter -ErrorAction Stop
            $freeGB = [math]::Round($drive.Free / 1GB, 1)
            if ($freeGB -lt $MinFreeGB) {
                Add-Result 'disk-space' 'FAIL' "$freeGB GB free on $driveLetter`:; need >= $MinFreeGB GB"
            }
            else {
                Add-Result 'disk-space' 'PASS' "$freeGB GB free on $driveLetter`:"
            }
        }
        else {
            Add-Result 'disk-space' 'WARN' 'could not resolve drive letter for ipc dir'
        }
    }
    catch {
        Add-Result 'disk-space' 'WARN' "could not stat drive: $($_.Exception.Message)"
    }
}
else {
    Add-Result 'disk-space' 'WARN' 'skipped (no slot env)'
}

# --- Step 6: StS2 process state --------------------------------------------
$gameProc = Get-Process -Name 'SlayTheSpire2' -ErrorAction SilentlyContinue
if ($gameProc) {
    $count = @($gameProc).Count
    Add-Result 'sts2-process' 'WARN' "$count instance(s) running; launcher prefers a fresh start"
}
else {
    Add-Result 'sts2-process' 'PASS' 'no game running'
}

# --- Step 7: OpenCode CLI on PATH ------------------------------------------
$opencode = Get-Command opencode -ErrorAction SilentlyContinue
if ($opencode) {
    Add-Result 'opencode-cli' 'PASS' $opencode.Source
}
else {
    Add-Result 'opencode-cli' 'WARN' 'not on PATH; ok if you launch OpenCode another way'
}

# --- Summary ---------------------------------------------------------------
""
"=== Slot $Slot preflight ==="
$pad = ($results.Check | Measure-Object -Maximum -Property Length).Maximum
foreach ($r in $results) {
    $color = switch ($r.Status) {
        'PASS' { 'Green' }
        'WARN' { 'Yellow' }
        'FAIL' { 'Red' }
    }
    $line = "  [{0}] {1,-$pad} : {2}" -f $r.Status, $r.Check, $r.Detail
    Write-Host $line -ForegroundColor $color
}

$failCount = ($results | Where-Object Status -eq 'FAIL').Count
$warnCount = ($results | Where-Object Status -eq 'WARN').Count
""
if ($failCount -gt 0) {
    Write-Host "Result: FAIL ($failCount failure(s), $warnCount warning(s))" -ForegroundColor Red
    exit 1
}
elseif ($warnCount -gt 0) {
    Write-Host "Result: PASS with $warnCount warning(s)" -ForegroundColor Yellow
    exit 0
}
else {
    Write-Host "Result: PASS" -ForegroundColor Green
    exit 0
}
