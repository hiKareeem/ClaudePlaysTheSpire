# tools/maintainer/append-run-csv.ps1
# ---------------------------------------------------------------
# Build a SpireBench runs.csv row from a run-record .md file's
# YAML front-matter + token data from get-session-tokens.ps1, and
# append it to docs/benchmark/runs.csv.
#
# This is the canonical operator-side step. The agent fills the
# .md frontmatter with everything it knows (run_id, character,
# halt_reason, death_floor, final_hp, final_gold, etc.). The
# operator runs this helper post-run to merge in token usage
# from the OpenCode session DB and append a typed, properly
# quoted CSV row in the correct column order.
#
# Usage:
#   .\tools\maintainer\append-run-csv.ps1 -RunId 2026-04-27-glm-5.1-ironclad-run01
#
# Optional:
#   -RecordPath   Override default docs/benchmark/runs/<RunId>.md
#   -CsvPath      Override default docs/benchmark/runs.csv
#   -PatchRecord  Also patch null/missing token fields back into the
#                 .md frontmatter (default: $true)
#   -DryRun       Print the CSV row without writing anything
#
# Behaviour:
#   - Reads <RunId>.md frontmatter (between leading --- and second ---)
#   - Calls tools/maintainer/get-session-tokens.ps1 with frontmatter's
#     opencode_session_id to fetch token totals + wall_seconds
#     + step_finish_count (only if any of those fields are null in
#     the frontmatter; otherwise frontmatter wins)
#   - Validates frontmatter run_id matches the -RunId argument
#   - Refuses to append if the run_id already exists in runs.csv
#   - Writes the CSV row in the exact column order of the existing
#     header. Seed is quoted to avoid scientific-notation rounding.
#     Empty / null fields are written as the empty string.
#   - If -PatchRecord, also rewrites the .md frontmatter with the
#     resolved token values (so the .md and CSV stay in lockstep)
# ---------------------------------------------------------------

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RunId,

    [string]$RecordPath,
    [string]$CsvPath,
    [bool]$PatchRecord = $true,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

# Resolve repo root from this script's location (tools/ is a child).
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSCommandPath)

if (-not $RecordPath) {
    $RecordPath = Join-Path $repoRoot "docs\benchmark\runs\$RunId.md"
}
if (-not $CsvPath) {
    $CsvPath = Join-Path $repoRoot 'docs\benchmark\runs.csv'
}

if (-not (Test-Path $RecordPath)) { throw "Record not found: $RecordPath" }
if (-not (Test-Path $CsvPath))    { throw "runs.csv not found: $CsvPath" }

# --- Parse YAML frontmatter (---...--- block at top of file) ----
$lines = Get-Content -LiteralPath $RecordPath
if ($lines.Count -lt 2 -or $lines[0].Trim() -ne '---') {
    throw "Frontmatter must start with '---' on line 1: $RecordPath"
}
$endIdx = -1
for ($i = 1; $i -lt $lines.Count; $i++) {
    if ($lines[$i].Trim() -eq '---') { $endIdx = $i; break }
}
if ($endIdx -lt 0) { throw "Frontmatter terminator '---' not found: $RecordPath" }

$fm = [ordered]@{}
for ($i = 1; $i -lt $endIdx; $i++) {
    $line = $lines[$i]
    if ($line -match '^\s*$' -or $line -match '^\s*#') { continue }
    if ($line -match '^\s*([a-zA-Z_][a-zA-Z0-9_]*)\s*:\s*(.*?)\s*$') {
        $key = $Matches[1]
        $val = $Matches[2]
        # Strip surrounding quotes if present
        if ($val -match '^"(.*)"$') { $val = $Matches[1] }
        elseif ($val -match "^'(.*)'$") { $val = $Matches[1] }
        $fm[$key] = $val
    }
}

if ($fm['run_id'] -ne $RunId) {
    throw "Frontmatter run_id ($($fm['run_id'])) does not match -RunId ($RunId)"
}

# --- Resolve token fields: prefer frontmatter, fall back to script ---
$tokenFields = @(
    'tokens_in','tokens_out','tokens_cache_read','tokens_cache_write',
    'tokens_reasoning','tokens_total','cost_usd','wall_seconds','step_finish_count'
)

function Test-IsNullish($v) {
    return ($null -eq $v) -or ($v -eq '') -or ($v -eq 'null') -or ($v -eq '~')
}

$needTokens = $false
foreach ($f in $tokenFields) {
    if (Test-IsNullish $fm[$f]) { $needTokens = $true; break }
}

if ($needTokens) {
    $sessionId = $fm['opencode_session_id']
    if (Test-IsNullish $sessionId) {
        Write-Warning "opencode_session_id missing from frontmatter; cannot fetch tokens. Token fields will be empty."
    } else {
        $tokenScript = Join-Path $repoRoot 'tools\maintainer\get-session-tokens.ps1'
        if (-not (Test-Path $tokenScript)) {
            throw "get-session-tokens.ps1 not found at $tokenScript"
        }
        Write-Host "Fetching tokens for $sessionId ..."
        $tokenOutput = & $tokenScript -SessionId $sessionId
        foreach ($line in $tokenOutput) {
            if ($line -match '^([a-zA-Z_]+):\s*(.*?)\s*$') {
                $k = $Matches[1]; $v = $Matches[2]
                if ($tokenFields -contains $k) {
                    # Only overwrite if frontmatter was nullish
                    if (Test-IsNullish $fm[$k]) { $fm[$k] = $v }
                }
            }
        }
    }
}

# Normalise 'null' -> empty string for CSV output.
foreach ($k in @($fm.Keys)) {
    if ($fm[$k] -eq 'null' -or $fm[$k] -eq '~') { $fm[$k] = '' }
}

# --- Build CSV row in the order of the existing header --------
$header = (Get-Content -LiteralPath $CsvPath -TotalCount 1).Trim()
$cols = $header.Split(',')

function Format-Csv([string]$col, [string]$val) {
    if ($null -eq $val) { return '' }
    # Force seed to be a quoted string to dodge scientific notation
    # in spreadsheet imports.
    if ($col -eq 'seed' -and $val -ne '') {
        return '"' + ($val -replace '"','""') + '"'
    }
    # Quote anything that contains a comma, quote, or newline.
    if ($val -match '[",\r\n]') {
        return '"' + ($val -replace '"','""') + '"'
    }
    return $val
}

$cells = foreach ($c in $cols) { Format-Csv $c ([string]$fm[$c]) }
$row = ($cells -join ',')

# --- Duplicate-row guard --------------------------------------
$existing = Get-Content -LiteralPath $CsvPath
$dup = $existing | Where-Object { $_ -match "^$([regex]::Escape($RunId))," }
if ($dup) {
    Write-Warning "runs.csv already contains a row for $RunId. Refusing to append."
    Write-Host "Existing row:"
    Write-Host "  $dup"
    Write-Host "Would-be new row:"
    Write-Host "  $row"
    if (-not $DryRun) {
        throw "Duplicate run_id. Re-run with -DryRun to inspect, or remove the existing row first."
    }
}

if ($DryRun) {
    Write-Host "--- DRY RUN ---"
    Write-Host "Header: $header"
    Write-Host "Row:    $row"
    return
}

Add-Content -LiteralPath $CsvPath -Value $row
Write-Host "Appended row to $CsvPath"

# --- Optionally patch token fields back into frontmatter ------
if ($PatchRecord) {
    $patched = @()
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($i -gt 0 -and $i -lt $endIdx -and $lines[$i] -match '^\s*([a-zA-Z_][a-zA-Z0-9_]*)\s*:\s*(.*?)\s*$') {
            $k = $Matches[1]
            if ($tokenFields -contains $k) {
                $newVal = $fm[$k]
                if ($newVal -eq '') { $newVal = 'null' }
                $patched += "${k}: $newVal"
                continue
            }
        }
        $patched += $lines[$i]
    }
    Set-Content -LiteralPath $RecordPath -Value $patched -Encoding UTF8
    Write-Host "Patched token fields into frontmatter: $RecordPath"
}
