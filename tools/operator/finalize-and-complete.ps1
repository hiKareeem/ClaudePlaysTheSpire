<#
.SYNOPSIS
    Finalize a completed trial-v1 run: copy floor-history.jsonl from the
    slot's IPC dir into docs/benchmark/runs/, run finalize-run.ps1, and
    mark the schedule row completed.

.DESCRIPTION
    Operator runs this once per run after the agent has written its
    run-record markdown (docs/benchmark/runs/<run_id>.md) and pasted the
    OpenCode session id into the frontmatter.

    Steps:
      1. Resolve the schedule row by -RunId. Reconstruct the wall-clock
         run-id (<started_utc-date>-<model>-<character.lower>-run<NNN>)
         using the row's started_utc and the same Format-RunId rule as
         start-run.ps1.
      2. Verify docs/benchmark/runs/<wall_run_id>.md exists. The agent
         must have written it during the OpenCode session.
      3. Resolve the slot's IPC dir from SPIREBRIDGE_IPC_DIR_<Slot>
         (Process -> User -> Machine cascade). Fall back to the default
         (%APPDATA%\SlayTheSpire2\hermesbridge) with a warning.
      4. Copy <ipc>\floor-history.jsonl -> docs/benchmark/runs/<wall_run_id>.jsonl.
         Skip if the destination already exists, unless -Force.
      5. Invoke tools/maintainer/finalize-run.ps1 -RunId <wall_run_id>
         (the existing 3-step pipeline: append-run-csv, parse-run-history,
         regenerate-runs-csv). Skipped with -SkipFinalize.
      6. Re-acquire the schedule lock and mark the row
         status=completed, completed_utc=<UTC ISO-8601>.
      7. Print a banner with the run summary.

.PARAMETER RunId
    Schedule row run_id (1..150 integer column). Required. The
    wall-clock run-id is reconstructed from this row's data.

.PARAMETER Slot
    Optional override. If omitted, derived from the schedule row's
    slot_assigned column. Determines which SPIREBRIDGE_IPC_DIR_<slot>
    env var is read for the source jsonl path.

.PARAMETER ScheduleCsv
    Path to the schedule CSV. Defaults to
    docs/benchmark/trial-v1-schedule.csv at the repo root.

.PARAMETER Force
    Overwrite an existing destination jsonl. Default: skip if present.

.PARAMETER SkipFinalize
    Skip step 5 (the finalize-run.ps1 invocation). Useful if the run was
    already finalized but the schedule row was never marked complete.

.PARAMETER DryRun
    Forwarded to finalize-run.ps1 (-DryRun). Implies -WhatIf for the
    schedule mutation and the jsonl copy.

.PARAMETER WhatIf
    Show what would happen but do not mutate the CSV, copy the jsonl,
    or invoke finalize-run.ps1.

.EXAMPLE
    .\tools\operator\finalize-and-complete.ps1 -RunId 4
    Auto-derive slot from schedule, copy jsonl, run finalize-run, mark
    row completed.

.EXAMPLE
    .\tools\operator\finalize-and-complete.ps1 -RunId 4 -Slot A -Force
    Override slot to A, force-overwrite an existing jsonl.

.EXAMPLE
    .\tools\operator\finalize-and-complete.ps1 -RunId 4 -SkipFinalize
    Just copy jsonl and mark the schedule row completed (run was
    already finalized manually).

.NOTES
    Idempotent against partial completion: if runs.csv already has the
    row (finalize-run.ps1 step 1 self-skips) and the destination jsonl
    already exists (we skip the copy without -Force), the only mutation
    will be the schedule status flip. Re-running is safe.

    Slot-write race protection: the CSV mutation acquires
    FileShare.None on the schedule. If another start-run / finalize is
    mid-claim, this blocks (or fails fast with a friendly error).
#>

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [int]$RunId,

    [ValidatePattern('^[A-Z]$')]
    [string]$Slot,

    [string]$ScheduleCsv,

    [switch]$Force,

    [switch]$SkipFinalize,

    [switch]$DryRun
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
if (-not (Test-Path $ScheduleCsv)) {
    throw "Schedule CSV not found: $ScheduleCsv"
}

$RunsDir       = Join-Path $RepoRoot 'docs\benchmark\runs'
$FinalizeScript = Join-Path $RepoRoot 'tools\maintainer\finalize-run.ps1'

if (-not $SkipFinalize -and -not (Test-Path $FinalizeScript)) {
    throw "finalize-run.ps1 not found at $FinalizeScript"
}
if (-not (Test-Path $RunsDir)) {
    throw "Runs directory not found: $RunsDir"
}

# ---------------------------------------------------------------------
# Helpers (mirrors start-run.ps1 - keep semantics identical)
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
# 1. Look up the schedule row (read-only first; we mutate later)
# ---------------------------------------------------------------------
Write-Host "Reading schedule (exclusive lock): $ScheduleCsv" -ForegroundColor DarkGray

$csv = $null
try {
    $csv = Read-CsvLocked -Path $ScheduleCsv
} catch {
    if ($_.Exception.Message -match 'being used by another process|cannot access') {
        throw "Schedule CSV is locked by another process. Wait a few seconds and retry."
    }
    throw
}

# Release immediately - we just need a snapshot for resolution
$rowsSnapshot = $csv.Rows
$csv.Stream.Dispose()
$csv = $null

$row = $rowsSnapshot | Where-Object { [int]$_.run_id -eq $RunId } | Select-Object -First 1
if (-not $row) {
    throw "RunId $RunId not found in schedule."
}

if ($row.status -eq 'completed') {
    Write-Warning "RunId $RunId is already marked completed ($($row.completed_utc)). Continuing - will re-run finalize idempotently and refresh completed_utc."
}
if ($row.status -eq 'pending') {
    throw "RunId $RunId is still 'pending' in the schedule. Did you forget to start-run.ps1 first? Refusing to finalize a row that was never started."
}

$rowSlot = $row.slot_assigned
if (-not $Slot) {
    if (-not $rowSlot) {
        throw "RunId $RunId has no slot_assigned in schedule. Pass -Slot explicitly."
    }
    $Slot = $rowSlot
    Write-Host "Derived slot from schedule: $Slot" -ForegroundColor DarkGray
} elseif ($rowSlot -and $rowSlot -ne $Slot) {
    Write-Warning "Slot mismatch: schedule says '$rowSlot' but -Slot was '$Slot'. Using '$Slot' (operator override)."
}

if (-not $row.started_utc) {
    throw "RunId $RunId has no started_utc - cannot reconstruct wall run-id. Pass -ScheduleCsv override or fix the row manually."
}

# ---------------------------------------------------------------------
# 2. Reconstruct wall run-id using the row's started_utc DATE
# ---------------------------------------------------------------------
$startedDate = $null
try {
    $startedDate = [datetime]::Parse($row.started_utc, $null,
                                     [System.Globalization.DateTimeStyles]::RoundtripKind)
} catch {
    throw "Could not parse started_utc='$($row.started_utc)' on row $RunId. Expected ISO-8601 (yyyy-MM-ddTHH:mm:ssZ)."
}
$dateStamp = $startedDate.ToUniversalTime().ToString('yyyy-MM-dd')

$wallRunId = Format-RunId -DateStamp $dateStamp -Model $row.model `
                          -Character $row.character -Index $RunId

Write-Host "Wall run-id: $wallRunId" -ForegroundColor DarkGray

# ---------------------------------------------------------------------
# 3. Verify the run-record markdown exists
# ---------------------------------------------------------------------
$recordPath = Join-Path $RunsDir "$wallRunId.md"
if (-not (Test-Path $recordPath)) {
    throw @"
Run-record markdown not found: $recordPath

The agent must have written it from the run-record-template before
this script runs. Either:
  - the agent never wrote the record (re-run the agent or write it
    manually using docs/benchmark/run-record-template.md), OR
  - the run-id naming differs (verify started_utc / model / character
    in the schedule row matches the .md filename).
"@
}
Write-Host "Run-record found: $recordPath" -ForegroundColor DarkGray

# ---------------------------------------------------------------------
# 4. Resolve IPC dir + copy floor-history.jsonl
# ---------------------------------------------------------------------
$ipcDirVar = "SPIREBRIDGE_IPC_DIR_$Slot"
$ipcDir    = [Environment]::GetEnvironmentVariable($ipcDirVar, 'Process')
if (-not $ipcDir) { $ipcDir = [Environment]::GetEnvironmentVariable($ipcDirVar, 'User') }
if (-not $ipcDir) { $ipcDir = [Environment]::GetEnvironmentVariable($ipcDirVar, 'Machine') }
if (-not $ipcDir) {
    $ipcDir = Join-Path $env:APPDATA 'SlayTheSpire2\hermesbridge'
    Write-Warning "$ipcDirVar not set. Falling back to default IPC dir: $ipcDir"
}

$srcJsonl = Join-Path $ipcDir 'floor-history.jsonl'
$dstJsonl = Join-Path $RunsDir "$wallRunId.jsonl"

if (-not (Test-Path $srcJsonl)) {
    throw "Source floor-history.jsonl not found: $srcJsonl. Did StS2 / SpireBridge run for slot $Slot?"
}

$dstExists = Test-Path $dstJsonl
if ($dstExists -and -not $Force) {
    Write-Host "Destination jsonl already exists, skipping copy: $dstJsonl" -ForegroundColor Yellow
    Write-Host "  (Use -Force to overwrite.)" -ForegroundColor DarkGray
} else {
    if ($PSCmdlet.ShouldProcess($dstJsonl, "copy floor-history.jsonl from $srcJsonl")) {
        Copy-Item -LiteralPath $srcJsonl -Destination $dstJsonl -Force:$Force
        $size = (Get-Item -LiteralPath $dstJsonl).Length
        Write-Host "Copied jsonl: $dstJsonl ($('{0:N0}' -f $size) bytes)" -ForegroundColor Green
    }
}

# ---------------------------------------------------------------------
# 5. Invoke finalize-run.ps1 (existing 3-step pipeline)
# ---------------------------------------------------------------------
$finalizeRan = $false
if (-not $SkipFinalize) {
    if ($PSCmdlet.ShouldProcess($wallRunId, "run finalize-run.ps1 (append-run-csv + parse + regenerate)")) {
        Write-Host "Running finalize-run.ps1 -RunId $wallRunId" -ForegroundColor DarkGray
        $finalizeArgs = @('-RunId', $wallRunId)
        if ($DryRun) { $finalizeArgs += '-DryRun' }

        & $FinalizeScript @finalizeArgs
        $finalizeExit = $LASTEXITCODE
        if ($finalizeExit -ne 0) {
            throw "finalize-run.ps1 failed (exit $finalizeExit). Schedule row NOT marked complete."
        }
        $finalizeRan = $true
    }
} else {
    Write-Host "finalize-run.ps1 SKIPPED (-SkipFinalize)." -ForegroundColor Yellow
}

# ---------------------------------------------------------------------
# 6. Mark schedule row completed
# ---------------------------------------------------------------------
if ($PSCmdlet.ShouldProcess("schedule row $RunId", "mark completed")) {
    $csv = $null
    try {
        $csv = Read-CsvLocked -Path $ScheduleCsv
    } catch {
        if ($_.Exception.Message -match 'being used by another process|cannot access') {
            throw "Schedule CSV is locked by another process during completion mark. Re-run with -SkipFinalize once the other process releases."
        }
        throw
    }
    try {
        $target = $csv.Rows | Where-Object { [int]$_.run_id -eq $RunId } | Select-Object -First 1
        if (-not $target) {
            throw "RunId $RunId disappeared from schedule between read passes."
        }
        $target.status        = 'completed'
        $target.completed_utc = Get-UtcIsoStamp
        # Leave slot_assigned / started_utc as-is for audit trail.

        Write-CsvLocked -Stream $csv.Stream -HeaderLine $csv.HeaderLine `
                        -ColumnOrder $csv.Header -Rows $csv.Rows
    } finally {
        if ($csv -and $csv.Stream) { $csv.Stream.Dispose() }
    }
    Write-Host "Schedule row $RunId marked completed." -ForegroundColor Green
}

# ---------------------------------------------------------------------
# Banner
# ---------------------------------------------------------------------
Write-Banner -Title "FINALIZED - run $RunId / $wallRunId" -Color Green -Lines @(
    "  run_id        : $RunId"
    "  wall run-id   : $wallRunId"
    "  slot          : $Slot"
    "  model         : $($row.model)"
    "  character     : $($row.character)"
    "  prior         : $($row.prior)"
    "  record (md)   : $recordPath"
    "  history (jsonl): $dstJsonl"
    "  finalize-run  : $(if ($finalizeRan) { 'ran' } elseif ($SkipFinalize) { 'skipped (-SkipFinalize)' } else { 'not run (WhatIf?)' })"
    ''
    '  Next steps:'
    "    1. git add docs/benchmark/runs/$wallRunId.md docs/benchmark/runs/$wallRunId.jsonl docs/benchmark/runs/$wallRunId.run docs/benchmark/runs.csv docs/benchmark/trial-v1-schedule.csv"
    "    2. git commit -m `"data(benchmark): trial-v1 run $RunId ($($row.model) / $($row.character))`""
    "    3. .\tools\operator\start-run.ps1 -Slot $Slot   # claim next pending row"
)
