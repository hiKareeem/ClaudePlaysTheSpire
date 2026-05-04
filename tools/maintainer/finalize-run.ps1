# tools/maintainer/finalize-run.ps1
# ---------------------------------------------------------------
# One-shot post-run finalizer for SpireBench trial-v0.
#
# Replaces the 4-step ritual:
#   1. get-session-tokens.ps1      (auto-handled by append-run-csv)
#   2. append-run-csv.ps1          (CSV row + token frontmatter patch)
#   3. parse-run-history.py --write (archive .run + patch 21 stat fields)
#   4. regenerate-runs-csv.py      (rebuild CSV from frontmatter)
#
# Pre-requisites (must already be true in the .md frontmatter):
#   - run_id matches the file name
#   - opencode_session_id is set (NOT null)
#   - character is set (used by parse-run-history fallback matching)
#
# Usage:
#   .\tools\maintainer\finalize-run.ps1 -RunId 2026-05-01-gpt-5.5-regent-run13
#   .\tools\maintainer\finalize-run.ps1 -RecordPath docs\benchmark\runs\2026-05-01-gpt-5.5-regent-run13.md
#   .\tools\maintainer\finalize-run.ps1 -RunId ... -DryRun       # preview only
#   .\tools\maintainer\finalize-run.ps1 -RunId ... -SkipParse    # if .run already archived
#
# Exits non-zero on any step failure. Stops the pipeline on first
# error so you can fix and resume.
#
# Idempotency: safe to re-run after partial failure. If runs.csv already
# contains a row for the run_id, step 1 is auto-skipped and the script
# resumes from parse-run-history. Token frontmatter patching inside
# append-run-csv is itself idempotent (only fills nullish fields).
# ---------------------------------------------------------------

[CmdletBinding()]
param(
    [string]$RunId,
    [string]$RecordPath,
    [switch]$DryRun,
    [switch]$SkipAppend,
    [switch]$SkipParse,
    [switch]$SkipRegenerate
)

$ErrorActionPreference = 'Stop'

# Resolve repo root from this script's location (tools/maintainer/<this>.ps1 is three deep).
$repoRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSCommandPath))

# --- Resolve RunId vs RecordPath ---------------------------------
if (-not $RunId -and -not $RecordPath) {
    throw "Must supply either -RunId or -RecordPath"
}
if ($RecordPath -and -not $RunId) {
    $RunId = [System.IO.Path]::GetFileNameWithoutExtension($RecordPath)
}
if (-not $RecordPath) {
    $RecordPath = Join-Path $repoRoot "docs\benchmark\runs\$RunId.md"
}
if (-not (Test-Path $RecordPath)) {
    throw "Record not found: $RecordPath"
}

# --- Sanity check frontmatter has session id ---------------------
$fmText = Get-Content -LiteralPath $RecordPath -Raw
if ($fmText -notmatch '(?m)^opencode_session_id:\s*ses_\S+') {
    throw "Frontmatter $RecordPath has no valid opencode_session_id (paste it in before finalizing)"
}

$bar = '=' * 64
function Write-Step($n, $total, $label) {
    Write-Host ""
    Write-Host $bar -ForegroundColor DarkCyan
    Write-Host ("[{0}/{1}] {2}" -f $n, $total, $label) -ForegroundColor Cyan
    Write-Host $bar -ForegroundColor DarkCyan
}

$totalSteps = 3
if ($SkipAppend)     { $totalSteps -= 1 }
if ($SkipParse)      { $totalSteps -= 1 }
if ($SkipRegenerate) { $totalSteps -= 1 }
$step = 0

# --- 1. append-run-csv.ps1 (also resolves + patches tokens) ------
# Idempotency: if the CSV already has a row for this run_id, the underlying
# append-run-csv.ps1 will throw on its duplicate-row guard. That's correct
# behaviour for a one-shot invocation, but breaks resume-after-failure when
# step 2 or 3 erred on a previous attempt. So we pre-check here and skip
# step 1 cleanly when the row is already present (token frontmatter patch
# is idempotent on its own; re-running would just no-op).
if (-not $SkipAppend) {
    $csvPath = Join-Path $repoRoot 'docs\benchmark\runs.csv'
    $alreadyAppended = $false
    if (Test-Path $csvPath) {
        $existing = Get-Content -LiteralPath $csvPath
        if ($existing | Where-Object { $_ -match "^$([regex]::Escape($RunId))," }) {
            $alreadyAppended = $true
        }
    }
    if ($alreadyAppended) {
        $totalSteps -= 1
        Write-Host ""
        Write-Host "[skip] append-run-csv.ps1 -- runs.csv already has row for $RunId" -ForegroundColor Yellow
        Write-Host "       (resuming from parse step; pass -SkipAppend explicitly to silence this)" -ForegroundColor DarkGray
    } else {
        $step++
        Write-Step $step $totalSteps "append-run-csv.ps1 (CSV row + token patch)"
        $appendArgs = @{ RunId = $RunId }
        if ($DryRun) { $appendArgs['DryRun'] = $true }
        & (Join-Path $repoRoot 'tools\maintainer\append-run-csv.ps1') @appendArgs
        if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
            throw "append-run-csv.ps1 failed with exit $LASTEXITCODE"
        }
    }
}

# --- 2. parse-run-history.py --write (archive .run + 21 stats) ---
if (-not $SkipParse) {
    $step++
    Write-Step $step $totalSteps "parse-run-history.py (archive .run + patch stats)"
    $parseArgs = @('tools\maintainer\parse-run-history.py', '--run-id', $RunId)
    if (-not $DryRun) { $parseArgs += '--write' }
    Push-Location $repoRoot
    try {
        & python @parseArgs
        if ($LASTEXITCODE -ne 0) {
            throw "parse-run-history.py failed with exit $LASTEXITCODE"
        }
    } finally {
        Pop-Location
    }
}

# --- 3. regenerate-runs-csv.py (rebuild CSV from frontmatter) ----
if (-not $SkipRegenerate) {
    $step++
    Write-Step $step $totalSteps "regenerate-runs-csv.py (rebuild CSV from all frontmatter)"
    $regenArgs = @('tools\maintainer\regenerate-runs-csv.py')
    if ($DryRun) { $regenArgs += '--dry-run' }
    Push-Location $repoRoot
    try {
        & python @regenArgs
        if ($LASTEXITCODE -ne 0) {
            throw "regenerate-runs-csv.py failed with exit $LASTEXITCODE"
        }
    } finally {
        Pop-Location
    }
}

Write-Host ""
Write-Host $bar -ForegroundColor Green
if ($DryRun) {
    Write-Host "DRY RUN COMPLETE for $RunId (no changes written)" -ForegroundColor Green
} else {
    Write-Host "FINALIZED $RunId" -ForegroundColor Green
}
Write-Host $bar -ForegroundColor Green
