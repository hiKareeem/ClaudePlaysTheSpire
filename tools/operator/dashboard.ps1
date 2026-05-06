#requires -Version 5.1
<#
.SYNOPSIS
    One-shot trial-v1 progress dashboard. Reads the schedule CSV and prints
    a human-readable summary of progress by status, model, character, and
    in-progress runs.

.DESCRIPTION
    Read-only. Does NOT lock the schedule (uses streaming read to coexist
    with start-run.ps1 / finalize-and-complete.ps1 holding the FileShare.None
    lock briefly). If the read collides, retries once.

    Reports:
      - Overall status counts (pending / in_progress / completed)
      - Per-model rollup (completed/total)
      - Per-character rollup (completed/total)
      - In-progress runs with elapsed wall time
      - ETA based on mean completed-run duration (if >=3 completed)

    No writes, no I/O outside of the schedule CSV. Operator can run this
    any time without disrupting active slots.

.PARAMETER ScheduleCsv
    Path to the schedule CSV. Default: docs/benchmark/trial-v1-schedule.csv
    relative to the script's parent (repo root).

.EXAMPLE
    .\dashboard.ps1

    Print one-shot progress summary.

.EXAMPLE
    .\dashboard.ps1 -ScheduleCsv D:\some\other\schedule.csv

    Override schedule path.
#>

[CmdletBinding()]
param(
    [string]$ScheduleCsv
)

$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Resolve schedule path
# ---------------------------------------------------------------------------

if (-not $ScheduleCsv) {
    $repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    $ScheduleCsv = Join-Path $repoRoot 'docs\benchmark\trial-v1-schedule.csv'
}

if (-not (Test-Path -LiteralPath $ScheduleCsv)) {
    Write-Error "Schedule CSV not found: $ScheduleCsv"
    exit 1
}

# ---------------------------------------------------------------------------
# Read CSV without locking (start-run.ps1 / finalize-and-complete.ps1 hold
# FileShare.None for ms; retry once on collision)
# ---------------------------------------------------------------------------

function Read-CsvUnlocked {
    param([string]$Path)

    for ($attempt = 0; $attempt -lt 3; $attempt++) {
        try {
            return Import-Csv -LiteralPath $Path
        }
        catch {
            if ($attempt -eq 2) { throw }
            Start-Sleep -Milliseconds 250
        }
    }
}

$rows = Read-CsvUnlocked -Path $ScheduleCsv
if ($null -eq $rows -or $rows.Count -eq 0) {
    Write-Error "Schedule is empty: $ScheduleCsv"
    exit 1
}

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

function Get-Pct {
    param([int]$Num, [int]$Denom)
    if ($Denom -le 0) { return '  -%' }
    return ('{0,4:N0}%' -f (100.0 * $Num / $Denom))
}

function Format-Duration {
    param([TimeSpan]$Span)
    if ($Span.TotalDays -ge 1) {
        return ('{0}d {1:D2}h {2:D2}m' -f [int]$Span.TotalDays, $Span.Hours, $Span.Minutes)
    }
    if ($Span.TotalHours -ge 1) {
        return ('{0}h {1:D2}m' -f [int]$Span.TotalHours, $Span.Minutes)
    }
    return ('{0}m {1:D2}s' -f [int]$Span.TotalMinutes, $Span.Seconds)
}

function Parse-Utc {
    param([string]$Iso)
    if ([string]::IsNullOrWhiteSpace($Iso)) { return $null }
    try {
        return [datetime]::Parse($Iso, [System.Globalization.CultureInfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::RoundtripKind).ToUniversalTime()
    }
    catch { return $null }
}

# ---------------------------------------------------------------------------
# Aggregate
# ---------------------------------------------------------------------------

$total       = $rows.Count
$pending     = @($rows | Where-Object { $_.status -eq 'pending' })
$inProgress  = @($rows | Where-Object { $_.status -eq 'in_progress' })
$completed   = @($rows | Where-Object { $_.status -eq 'completed' })

$nowUtc = [datetime]::UtcNow

# Mean completed-run wall time
$completedDurations = @()
foreach ($row in $completed) {
    $start = Parse-Utc $row.started_utc
    $end   = Parse-Utc $row.completed_utc
    if ($start -and $end -and $end -gt $start) {
        $completedDurations += ($end - $start).TotalMinutes
    }
}
$meanRunMin = if ($completedDurations.Count -ge 1) {
    [math]::Round(($completedDurations | Measure-Object -Average).Average, 1)
} else { $null }

# ---------------------------------------------------------------------------
# Output
# ---------------------------------------------------------------------------

$bar = '=' * 70
Write-Host ''
Write-Host $bar -ForegroundColor Cyan
Write-Host (' trial-v1 dashboard  -  {0}' -f $nowUtc.ToString('yyyy-MM-dd HH:mm:ss UTC')) -ForegroundColor Cyan
Write-Host (' schedule: {0}' -f $ScheduleCsv) -ForegroundColor DarkGray
Write-Host $bar -ForegroundColor Cyan
Write-Host ''

# ---- overall ----
Write-Host 'Overall progress' -ForegroundColor Yellow
Write-Host ('  completed  : {0,3} / {1,3}  ({2})' -f $completed.Count,  $total, (Get-Pct $completed.Count  $total)) -ForegroundColor Green
Write-Host ('  in_progress: {0,3} / {1,3}  ({2})' -f $inProgress.Count, $total, (Get-Pct $inProgress.Count $total)) -ForegroundColor Cyan
Write-Host ('  pending    : {0,3} / {1,3}  ({2})' -f $pending.Count,    $total, (Get-Pct $pending.Count    $total)) -ForegroundColor DarkGray
Write-Host ''

# ---- by model ----
Write-Host 'By model' -ForegroundColor Yellow
$rows | Group-Object model | Sort-Object Name | ForEach-Object {
    $g = $_
    $cDone = @($g.Group | Where-Object { $_.status -eq 'completed' }).Count
    $cTotal = $g.Count
    Write-Host ('  {0,-32} {1,3} / {2,3}  ({3})' -f $g.Name, $cDone, $cTotal, (Get-Pct $cDone $cTotal))
}
Write-Host ''

# ---- by character ----
Write-Host 'By character' -ForegroundColor Yellow
$rows | Group-Object character | Sort-Object Name | ForEach-Object {
    $g = $_
    $cDone = @($g.Group | Where-Object { $_.status -eq 'completed' }).Count
    $cTotal = $g.Count
    Write-Host ('  {0,-12} {1,3} / {2,3}  ({3})' -f $g.Name, $cDone, $cTotal, (Get-Pct $cDone $cTotal))
}
Write-Host ''

# ---- by prior ----
Write-Host 'By prior' -ForegroundColor Yellow
$rows | Group-Object prior | Sort-Object Name | ForEach-Object {
    $g = $_
    $cDone = @($g.Group | Where-Object { $_.status -eq 'completed' }).Count
    $cTotal = $g.Count
    Write-Host ('  {0,-6} {1,3} / {2,3}  ({3})' -f $g.Name, $cDone, $cTotal, (Get-Pct $cDone $cTotal))
}
Write-Host ''

# ---- in-progress detail ----
if ($inProgress.Count -gt 0) {
    Write-Host 'In-progress runs' -ForegroundColor Yellow
    foreach ($row in $inProgress | Sort-Object { [int]$_.run_id }) {
        $start = Parse-Utc $row.started_utc
        $elapsed = if ($start) { Format-Duration ($nowUtc - $start) } else { '?' }
        $slot = if ($row.slot_assigned) { $row.slot_assigned } else { '?' }
        Write-Host ('  run {0,3}  slot {1}  {2,-32} {3,-12} {4,-3}  elapsed {5}' -f `
            $row.run_id, $slot, $row.model, $row.character, $row.prior, $elapsed)
    }
    Write-Host ''
}

# ---- ETA ----
if ($meanRunMin -and $pending.Count -gt 0) {
    Write-Host 'ETA' -ForegroundColor Yellow
    Write-Host ('  mean completed-run duration : {0} min  (n={1})' -f $meanRunMin, $completedDurations.Count)
    $remainMin = $meanRunMin * $pending.Count
    $etaSpan = [TimeSpan]::FromMinutes($remainMin)
    Write-Host ('  serial ETA (1 slot)         : {0}' -f (Format-Duration $etaSpan))
    if ($inProgress.Count -gt 0) {
        $parallelMin = $remainMin / [Math]::Max($inProgress.Count, 1)
        $parallelSpan = [TimeSpan]::FromMinutes($parallelMin)
        Write-Host ('  parallel ETA ({0} slots)       : {1}' -f $inProgress.Count, (Format-Duration $parallelSpan))
    }
    Write-Host ''
} elseif ($pending.Count -eq 0 -and $inProgress.Count -eq 0) {
    Write-Host 'Trial complete: all 150 rows finalized.' -ForegroundColor Green
    Write-Host ''
} else {
    Write-Host ('ETA: not enough completed runs yet (need >=1 with timestamps).') -ForegroundColor DarkGray
    Write-Host ''
}

Write-Host $bar -ForegroundColor Cyan
