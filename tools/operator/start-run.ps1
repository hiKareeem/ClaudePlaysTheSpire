<#
.SYNOPSIS
    Claim the next pending row in the trial-v1 schedule for a slot, run
    the operator preflight, fill the prompt template, copy it to the
    clipboard, and spawn a trace-tail window.

.DESCRIPTION
    Operator runs this once per slot per run. It does the bookkeeping
    + prep so the only thing left is "paste prompt into a fresh
    OpenCode session and press enter".

    Steps:
      1. Acquire an exclusive lock on the schedule CSV (FileShare.None
         while reading) so two slot launches cannot grab the same row.
      2. Parse rows; pick the first row matching `-RunId` (if given) or
         else the first `status=pending` row.
      3. Mutate that row: slot_assigned=<Slot>, status=in_progress,
         started_utc=<UTC ISO-8601>. Write CSV back atomically.
      4. Run tools/operator/preflight-slot.ps1 -Slot <Slot>. Abort on
         non-zero exit (and revert the row mutation).
      5. Build the wall-clock run-id (e.g.
         2026-05-06-claude-opus-4.7-ironclad-run047) and select the
         prompt template by the row's `prior` column (A0 -> v1-A0.md,
         B0 -> v1-B0.md).
      6. Substitute <RUN_ID>, <CHARACTER>, <MODEL_SLUG>, <SEED> into the
         template body and copy the result to the clipboard via
         Set-Clipboard.
      7. Spawn a new pwsh window titled
         "[slot <Slot>] run<NN> <model>/<character>" tailing the slot's
         trace.log via Get-Content -Wait.
      8. Print a banner with the run details and the operator's next
         actions ("paste into OpenCode", "game must be on title screen").

.PARAMETER Slot
    Slot label (single uppercase letter, A..Z). Determines which
    SPIREBRIDGE_IPC_DIR_<slot> env var is read for the trace path.

.PARAMETER RunId
    Optional: claim a specific run_id (the integer 1..150 column in the
    schedule, NOT the wall-clock filesystem id). If omitted, the next
    pending row is claimed.

.PARAMETER ScheduleCsv
    Path to the schedule CSV. Defaults to
    docs/benchmark/trial-v1-schedule.csv at the repo root.

.PARAMETER PromptDir
    Directory containing agent-prompt-v1-A0.md / agent-prompt-v1-B0.md.
    Defaults to docs/benchmark at the repo root.

.PARAMETER NoTail
    Skip spawning the trace-tail window. Useful for tests / dry runs.

.PARAMETER NoPreflight
    Skip the preflight-slot.ps1 invocation. Strongly discouraged for
    real runs; provided for unit-test seams.

.PARAMETER WhatIf
    Show what would happen but do not mutate the CSV, copy the
    clipboard, or spawn windows.

.EXAMPLE
    .\tools\operator\start-run.ps1 -Slot A
    Claim the next pending row for slot A; run preflight; prep the
    prompt; tail trace.

.EXAMPLE
    .\tools\operator\start-run.ps1 -Slot B -RunId 47
    Claim run_id 47 specifically (operator override).

.EXAMPLE
    .\tools\operator\start-run.ps1 -Slot A -WhatIf
    Show the next pending row and the run-id that would be assembled,
    without touching anything.

.NOTES
    Designed to be safe to interrupt. If the script aborts AFTER the
    CSV is mutated but BEFORE preflight passes, it reverts the row to
    pending. If it aborts after preflight (e.g. clipboard / window
    spawn failed), the row stays in_progress and the operator can rerun
    with -RunId <N> after fixing the issue.

    Slot-write race protection: the CSV is opened with FileShare.None.
    A second start-run invocation against the same CSV will block (or
    fail fast with a friendly error) until the first releases.
#>

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Z]$')]
    [string]$Slot,

    [int]$RunId = 0,

    [string]$ScheduleCsv,

    [string]$PromptDir,

    [switch]$NoTail,

    [switch]$NoPreflight
)

$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------
# Resolve repo root + defaults
# ---------------------------------------------------------------------
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot  = Resolve-Path (Join-Path $ScriptDir '..\..') | Select-Object -ExpandProperty Path

if (-not $ScheduleCsv) {
    $ScheduleCsv = Join-Path $RepoRoot 'docs\benchmark\trial-v1-schedule.csv'
}
if (-not $PromptDir) {
    $PromptDir = Join-Path $RepoRoot 'docs\benchmark'
}

if (-not (Test-Path $ScheduleCsv)) {
    throw "Schedule CSV not found: $ScheduleCsv"
}
if (-not (Test-Path $PromptDir)) {
    throw "Prompt directory not found: $PromptDir"
}

$PreflightScript = Join-Path $RepoRoot 'tools\operator\preflight-slot.ps1'
if (-not $NoPreflight -and -not (Test-Path $PreflightScript)) {
    throw "preflight-slot.ps1 not found at $PreflightScript"
}

# ---------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------
function Write-Banner {
    param([string]$Title, [string[]]$Lines, [ConsoleColor]$Color = 'Cyan')
    $width = 70
    $bar = ('=' * $width)
    Write-Host ''
    Write-Host $bar -ForegroundColor $Color
    Write-Host (' ' + $Title) -ForegroundColor $Color
    Write-Host $bar -ForegroundColor $Color
    foreach ($l in $Lines) { Write-Host $l }
    Write-Host $bar -ForegroundColor $Color
    Write-Host ''
}

function Read-CsvLocked {
    <#
    Open the CSV with FileShare.None, read all bytes, return:
      @{ Stream = <FileStream>; Rows = [object[]]; HeaderLine = <string>; Header = <string[]> }
    Caller is responsible for disposing the stream (which releases the lock).
    #>
    param([string]$Path)

    $fs = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open,
                                 [System.IO.FileAccess]::ReadWrite,
                                 [System.IO.FileShare]::None)
    try {
        $reader = New-Object System.IO.StreamReader($fs, [System.Text.Encoding]::UTF8, $true, 4096, $true)
        $text = $reader.ReadToEnd()
        $reader.Dispose()
    } catch {
        $fs.Dispose()
        throw
    }

    $lines = $text -split "`r?`n"
    # Drop trailing empty line if present
    while ($lines.Count -gt 0 -and [string]::IsNullOrEmpty($lines[-1])) {
        $lines = $lines[0..($lines.Count - 2)]
    }
    if ($lines.Count -lt 1) {
        $fs.Dispose()
        throw "Schedule CSV is empty: $Path"
    }
    $headerLine = $lines[0]
    $header = $headerLine -split ','

    $rows = New-Object 'System.Collections.Generic.List[object]'
    for ($i = 1; $i -lt $lines.Count; $i++) {
        $cells = $lines[$i] -split ','
        $row = [ordered]@{}
        for ($j = 0; $j -lt $header.Count; $j++) {
            $row[$header[$j]] = if ($j -lt $cells.Count) { $cells[$j] } else { '' }
        }
        [void]$rows.Add([pscustomobject]$row)
    }

    return @{
        Stream     = $fs
        Rows       = $rows
        Header     = $header
        HeaderLine = $headerLine
    }
}

function Write-CsvLocked {
    <#
    Truncate + rewrite the CSV through the still-locked stream so the
    update is atomic relative to other start-run.ps1 invocations.
    #>
    param(
        [System.IO.FileStream]$Stream,
        [string]$HeaderLine,
        [string[]]$ColumnOrder,
        [object[]]$Rows
    )

    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine($HeaderLine)
    foreach ($row in $Rows) {
        $cells = foreach ($col in $ColumnOrder) { [string]$row.$col }
        [void]$sb.AppendLine(($cells -join ','))
    }
    # Strip trailing newline added by last AppendLine to match the
    # generator's "no trailing blank line" convention. Keep one final
    # newline though (POSIX convention).
    $payload = $sb.ToString()
    if ($payload.EndsWith("`r`n`r`n")) {
        $payload = $payload.Substring(0, $payload.Length - 2)
    }

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
    $Stream.Position = 0
    $Stream.SetLength(0)
    $Stream.Write($bytes, 0, $bytes.Length)
    $Stream.Flush()
}

function Get-DateStamp {
    return [DateTime]::UtcNow.ToString('yyyy-MM-dd')
}

function Get-UtcIsoStamp {
    return [DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
}

function Format-RunId {
    param(
        [string]$DateStamp,
        [string]$Model,
        [string]$Character,
        [int]$Index
    )
    $charLower = $Character.ToLowerInvariant()
    $idx = '{0:D3}' -f $Index
    return "$DateStamp-$Model-$charLower-run$idx"
}

# ---------------------------------------------------------------------
# Acquire schedule + claim a row
# ---------------------------------------------------------------------
Write-Host "Reading schedule (exclusive lock): $ScheduleCsv" -ForegroundColor DarkGray

$csv = $null
try {
    $csv = Read-CsvLocked -Path $ScheduleCsv
} catch {
    if ($_.Exception.Message -match 'being used by another process|cannot access') {
        throw "Schedule CSV is locked by another process. A different start-run.ps1 is mid-claim. Wait a few seconds and retry."
    }
    throw
}

try {
    $rows = $csv.Rows

    # Pick the row to claim
    $target = $null
    if ($RunId -gt 0) {
        $target = $rows | Where-Object { [int]$_.run_id -eq $RunId } | Select-Object -First 1
        if (-not $target) {
            throw "RunId $RunId not found in schedule."
        }
        if ($target.status -eq 'completed') {
            throw "RunId $RunId is already completed. Refusing to re-claim."
        }
        if ($target.status -eq 'in_progress') {
            Write-Warning "RunId $RunId is already in_progress (slot $($target.slot_assigned)). Reclaiming for slot $Slot."
        }
    } else {
        $target = $rows | Where-Object { $_.status -eq 'pending' } | Select-Object -First 1
        if (-not $target) {
            throw "No pending rows remain in schedule. All 150 trial-v1 cells are either in_progress or completed."
        }
    }

    $claimedRunId    = [int]$target.run_id
    $claimedModel    = $target.model
    $claimedCharacter= $target.character
    $claimedPrior    = $target.prior
    $claimedKIndex   = $target.k_index
    $claimedSeed     = $target.seed

    if ($PSCmdlet.ShouldProcess("schedule row $claimedRunId", "claim for slot $Slot")) {
        $target.slot_assigned = $Slot
        $target.status        = 'in_progress'
        $target.started_utc   = Get-UtcIsoStamp
        # completed_utc stays empty until finalize-and-scaffold.ps1 fills it

        Write-CsvLocked -Stream $csv.Stream -HeaderLine $csv.HeaderLine -ColumnOrder $csv.Header -Rows $rows
    }
} catch {
    if ($csv -and $csv.Stream) { $csv.Stream.Dispose() }
    throw
}

# Release the lock now that the claim is persisted
$csv.Stream.Dispose()
$csv = $null

# ---------------------------------------------------------------------
# Preflight
# ---------------------------------------------------------------------
if (-not $NoPreflight) {
    Write-Host "Running preflight: $PreflightScript -Slot $Slot" -ForegroundColor DarkGray

    & $PreflightScript -Slot $Slot
    $preflightExit = $LASTEXITCODE
    if ($preflightExit -ne 0) {
        Write-Warning "Preflight FAILED (exit $preflightExit). Reverting schedule row $claimedRunId to pending."

        # Re-acquire and revert. Best-effort; if this fails we surface both errors.
        try {
            $csv = Read-CsvLocked -Path $ScheduleCsv
            $revertRow = $csv.Rows | Where-Object { [int]$_.run_id -eq $claimedRunId } | Select-Object -First 1
            if ($revertRow) {
                $revertRow.slot_assigned = ''
                $revertRow.status        = 'pending'
                $revertRow.started_utc   = ''
                Write-CsvLocked -Stream $csv.Stream -HeaderLine $csv.HeaderLine -ColumnOrder $csv.Header -Rows $csv.Rows
            }
            $csv.Stream.Dispose()
        } catch {
            Write-Warning "Revert failed: $($_.Exception.Message)"
            Write-Warning "Manually edit $ScheduleCsv : set run_id=$claimedRunId status=pending, slot_assigned='', started_utc=''."
        }
        throw "Preflight failed for slot $Slot. Schedule row $claimedRunId reverted."
    }
} else {
    Write-Host "Preflight SKIPPED (-NoPreflight)." -ForegroundColor Yellow
}

# ---------------------------------------------------------------------
# Build the wall-clock run-id and substitute the prompt
# ---------------------------------------------------------------------
$dateStamp = Get-DateStamp
$wallRunId = Format-RunId -DateStamp $dateStamp -Model $claimedModel `
                          -Character $claimedCharacter -Index $claimedRunId

$promptFile = switch ($claimedPrior) {
    'A0' { Join-Path $PromptDir 'agent-prompt-v1-A0.md' }
    'B0' { Join-Path $PromptDir 'agent-prompt-v1-B0.md' }
    default { throw "Unknown prior '$claimedPrior' on row $claimedRunId. Expected A0 or B0." }
}
if (-not (Test-Path $promptFile)) {
    throw "Prompt template not found: $promptFile"
}

$promptText = Get-Content -Raw -LiteralPath $promptFile

# Slice between the "Copy from here" and "Copy to here" markers so the
# operator-facing header (purpose, placeholder legend, "for B0 use the
# other file" notes) is NOT pasted into the agent's first message. Only
# the contract body the agent should see is sent.
#
# Markers are matched as ATX headings starting with one or more `#`,
# followed by any whitespace, then the literal phrase. This is forgiving
# of stray double-spaces that have crept into templates historically.
$startRegex = [regex]'(?m)^#+\s+Copy\s+from\s+here\b.*$'
$endRegex   = [regex]'(?m)^#+\s+Copy\s+to\s+here\b.*$'
$startMatch = $startRegex.Match($promptText)
$endMatch   = $endRegex.Match($promptText)
if (-not $startMatch.Success -or -not $endMatch.Success -or $endMatch.Index -le $startMatch.Index) {
    throw "Prompt template '$promptFile' is missing a 'Copy from here' / 'Copy to here' heading pair; cannot slice agent-visible body."
}
# Body starts on the line AFTER the start marker, ends BEFORE the end
# marker. Trim trailing whitespace and re-add a single trailing newline.
$bodyStart = $startMatch.Index + $startMatch.Length
# Skip the newline immediately following the start-marker line, if any.
if ($bodyStart -lt $promptText.Length -and $promptText[$bodyStart] -eq "`r") { $bodyStart++ }
if ($bodyStart -lt $promptText.Length -and $promptText[$bodyStart] -eq "`n") { $bodyStart++ }
$promptText = $promptText.Substring($bodyStart, $endMatch.Index - $bodyStart).Trim() + "`r`n"

# Plain string replacement (not -replace) so values containing regex
# metacharacters or `$1`-style backreferences are handled literally.
# Order matters: substitute <CHARACTER> first so any leftover
# placeholder inside <RUN_ID> would still be visible if generation
# went wrong (sanity-checked at the end).
$filled = $promptText.Replace('<CHARACTER>',  $claimedCharacter)
$filled = $filled.Replace('<MODEL_SLUG>',     $claimedModel)
$filled = $filled.Replace('<SEED>',           $claimedSeed)
$filled = $filled.Replace('<RUN_ID>',         $wallRunId)

if ($filled -match '<[A-Z_]+>') {
    Write-Warning "Filled prompt still contains a placeholder. Inspect manually before pasting."
    $remaining = [regex]::Matches($filled, '<[A-Z_]+>') | ForEach-Object { $_.Value } | Sort-Object -Unique
    Write-Warning "  Remaining placeholders: $($remaining -join ', ')"
}

# ---------------------------------------------------------------------
# Copy to clipboard
# ---------------------------------------------------------------------
if ($PSCmdlet.ShouldProcess('clipboard', 'copy filled prompt')) {
    try {
        Set-Clipboard -Value $filled
        Write-Host "Prompt copied to clipboard ($('{0:N0}' -f $filled.Length) chars)." -ForegroundColor Green
    } catch {
        Write-Warning "Set-Clipboard failed: $($_.Exception.Message). Prompt written to a temp file instead."
        $tmp = Join-Path $env:TEMP "spirebench-prompt-$wallRunId.md"
        $filled | Set-Content -LiteralPath $tmp -Encoding utf8
        Write-Host "  Prompt at: $tmp" -ForegroundColor Yellow
    }
}

# ---------------------------------------------------------------------
# Spawn trace-tail window
# ---------------------------------------------------------------------
$tracePath = $null
$ipcDirVar = "SPIREBRIDGE_IPC_DIR_$Slot"
$ipcDir    = [Environment]::GetEnvironmentVariable($ipcDirVar, 'Process')
if (-not $ipcDir) { $ipcDir = [Environment]::GetEnvironmentVariable($ipcDirVar, 'User') }
if (-not $ipcDir) { $ipcDir = [Environment]::GetEnvironmentVariable($ipcDirVar, 'Machine') }
if ($ipcDir) {
    $tracePath = Join-Path $ipcDir 'trace.log'
} else {
    # Fall back to default; preflight should have caught this
    $defaultDir = Join-Path $env:APPDATA 'SlayTheSpire2\hermesbridge'
    $tracePath = Join-Path $defaultDir 'trace.log'
    Write-Warning "$ipcDirVar not set. Falling back to default trace path: $tracePath"
}

if (-not $NoTail) {
    if ($PSCmdlet.ShouldProcess($tracePath, "spawn trace-tail window")) {
        if (-not (Test-Path $tracePath)) {
            Write-Warning "Trace not yet present at $tracePath (will appear after StS2 launches)."
        }
        $title = "[slot $Slot] run$('{0:D3}' -f $claimedRunId) $claimedModel/$claimedCharacter"
        $tailCmd = "`$Host.UI.RawUI.WindowTitle = '$title'; Get-Content -LiteralPath '$tracePath' -Wait -Tail 0"
        try {
            Start-Process -FilePath 'pwsh' -ArgumentList @(
                '-NoExit',
                '-NoProfile',
                '-Command', $tailCmd
            ) | Out-Null
            Write-Host "Trace tail window spawned: $title" -ForegroundColor Green
        } catch {
            Write-Warning "Failed to spawn trace-tail window: $($_.Exception.Message)"
            Write-Host "  Run manually:  pwsh -NoExit -Command `"Get-Content -LiteralPath '$tracePath' -Wait -Tail 0`"" -ForegroundColor Yellow
        }
    }
}

# ---------------------------------------------------------------------
# Banner
# ---------------------------------------------------------------------
Write-Banner -Title "SLOT $Slot - run $claimedRunId ($claimedPrior)" -Color Cyan -Lines @(
    "  run_id      : $claimedRunId  (k=$claimedKIndex)"
    "  wall run-id : $wallRunId"
    "  model       : $claimedModel"
    "  character   : $claimedCharacter"
    "  prior       : $claimedPrior"
    "  seed        : $claimedSeed"
    "  prompt file : $(Split-Path -Leaf $promptFile)"
    "  trace       : $tracePath"
    ''
    '  Next steps:'
    '    1. Confirm StS2 is on the title screen for this slot.'
    '    2. Open a FRESH OpenCode session (no carry-over context).'
    '    3. Paste the clipboard contents and press enter.'
    '    4. When the run terminates, run finalize-and-complete.ps1.'
)
