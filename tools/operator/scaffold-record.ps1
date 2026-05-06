<#
.SYNOPSIS
  Scaffold a trial-v1 run-record markdown from live IPC artifacts.

.DESCRIPTION
  Reads docs/benchmark/trial-v1-schedule.csv to resolve the run's
  metadata (model, character, prior, seed, slot, started_utc), then
  parses the slot's floor-history.jsonl + trace.log to derive every
  frontmatter field that doesn't require parse-run-history.py or the
  agent's prose.

  Produces docs/benchmark/runs/<wall_run_id>.md with:
    - All YAML frontmatter pre-filled except the parse-run-history.py
      stat block (those stay null and get patched by finalize-run later)
    - Empty Summary / Bridge findings / Decision log / Notes sections
      for the agent (or operator) to fill in

  The operator workflow is:
    1. Run completes (death / victory / stall / cap)
    2. .\tools\operator\scaffold-record.ps1 -RunId <N> -Slot A
    3. Operator pastes opencode_session_id into frontmatter
    4. Agent (in fresh OpenCode session) writes Summary + findings + decisions
    5. .\tools\operator\finalize-and-scaffold.ps1 (#5) marks complete +
       runs finalize-run.ps1 to fill stats, tokens, regenerate runs.csv

.PARAMETER RunId
  Schedule row index (1..150). Required.

.PARAMETER Slot
  Slot letter (A-Z). If omitted, derived from schedule row's
  slot_assigned column. Used to resolve SPIREBRIDGE_IPC_DIR_<slot>
  environment variable for the IPC directory.

.PARAMETER ScheduleCsv
  Path to schedule CSV. Defaults to docs/benchmark/trial-v1-schedule.csv

.PARAMETER OutputDir
  Where to write the record. Defaults to docs/benchmark/runs/

.PARAMETER IpcDir
  Override IPC dir (skips env-var lookup). Useful for offline replay.

.PARAMETER Force
  Overwrite an existing record file. Default: refuse.

.PARAMETER NoTraceCounts
  Skip trace.log scanning (faster on large logs; leaves command_count
  and ipc_error_count at 0).

.EXAMPLE
  .\tools\operator\scaffold-record.ps1 -RunId 4 -Slot A

.EXAMPLE
  .\tools\operator\scaffold-record.ps1 -RunId 12 -IpcDir D:\replay\run12
#>

[CmdletBinding(SupportsShouldProcess=$true)]
param(
    [Parameter(Mandatory=$true)]
    [int]$RunId,

    [ValidatePattern('^[A-Z]$')]
    [string]$Slot,

    [string]$ScheduleCsv,

    [string]$OutputDir,

    [string]$IpcDir,

    [switch]$Force,

    [switch]$NoTraceCounts
)

$ErrorActionPreference = 'Stop'

# Resolve repo root from this script's location: tools/operator/<this>.ps1
$RepoRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSCommandPath))

if (-not $ScheduleCsv) {
    $ScheduleCsv = Join-Path $RepoRoot 'docs\benchmark\trial-v1-schedule.csv'
}
if (-not $OutputDir) {
    $OutputDir = Join-Path $RepoRoot 'docs\benchmark\runs'
}

if (-not (Test-Path -LiteralPath $ScheduleCsv)) {
    throw "Schedule not found: $ScheduleCsv"
}
if (-not (Test-Path -LiteralPath $OutputDir)) {
    throw "Output dir not found: $OutputDir"
}

# ----------------------------------------------------------------
# Read schedule row (read-only, no lock needed)
# ----------------------------------------------------------------
$rows = Import-Csv -LiteralPath $ScheduleCsv
$row  = $rows | Where-Object { [int]$_.run_id -eq $RunId }
if (-not $row) {
    throw "RunId $RunId not found in schedule $ScheduleCsv"
}

$model      = $row.model
$character  = $row.character
$prior      = $row.prior
$seed       = $row.seed
$startedUtc = $row.started_utc
if (-not $Slot) {
    $Slot = $row.slot_assigned
}
if (-not $Slot) {
    throw "RunId $RunId has no slot_assigned and -Slot not supplied"
}
if (-not $startedUtc) {
    throw "RunId $RunId has no started_utc; was it ever started?"
}

# ----------------------------------------------------------------
# Reconstruct wall run-id (matches start-run.ps1 Format-RunId rule)
# ----------------------------------------------------------------
$dateStamp   = ([datetime]::Parse($startedUtc).ToUniversalTime()).ToString('yyyy-MM-dd')
$charLower   = $character.ToLowerInvariant()
$idxPadded   = '{0:D3}' -f $RunId
$wallRunId   = "$dateStamp-$model-$charLower-run$idxPadded"
$recordPath  = Join-Path $OutputDir "$wallRunId.md"

if ((Test-Path -LiteralPath $recordPath) -and -not $Force) {
    throw "Record already exists: $recordPath`nUse -Force to overwrite."
}

# ----------------------------------------------------------------
# Resolve IPC dir
# ----------------------------------------------------------------
if (-not $IpcDir) {
    $varName = "SPIREBRIDGE_IPC_DIR_$Slot"
    foreach ($scope in 'Process','User','Machine') {
        $val = [Environment]::GetEnvironmentVariable($varName, $scope)
        if ($val) { $IpcDir = $val; break }
    }
    if (-not $IpcDir) {
        $IpcDir = Join-Path $env:APPDATA 'SlayTheSpire2\hermesbridge'
        Write-Warning "SPIREBRIDGE_IPC_DIR_$Slot not set; falling back to default $IpcDir"
    }
}
if (-not (Test-Path -LiteralPath $IpcDir)) {
    throw "IPC dir not found: $IpcDir"
}

$jsonlPath = Join-Path $IpcDir 'floor-history.jsonl'
$tracePath = Join-Path $IpcDir 'trace.log'

if (-not (Test-Path -LiteralPath $jsonlPath)) {
    throw "floor-history.jsonl not found at $jsonlPath"
}

# ----------------------------------------------------------------
# Parse floor-history.jsonl
# Each line: {"t":"...Z","floor":N,"act":N,"hp":N,"maxHp":N,"gold":N,
#             "deckSize":N,"relicCount":N,"potionCount":N,"roomType":"..."}
# Filter to entries at or after $startedUtc (covers multi-run trace dirs).
# ----------------------------------------------------------------
$startedDt  = [datetime]::Parse($startedUtc).ToUniversalTime()
$entries    = @()
foreach ($line in Get-Content -LiteralPath $jsonlPath) {
    if (-not $line.Trim()) { continue }
    try {
        $obj = $line | ConvertFrom-Json -ErrorAction Stop
    } catch {
        Write-Warning "Skipping unparseable jsonl line: $line"
        continue
    }
    $t = [datetime]::Parse($obj.t).ToUniversalTime()
    if ($t -ge $startedDt) {
        $entries += [pscustomobject]@{
            t        = $t
            floor    = [int]$obj.floor
            act      = [int]$obj.act
            hp       = [int]$obj.hp
            maxHp    = [int]$obj.maxHp
            gold     = [int]$obj.gold
            roomType = $obj.roomType
        }
    }
}

if ($entries.Count -eq 0) {
    throw "No floor-history entries found at or after $startedUtc in $jsonlPath. Did the run actually start?"
}

$firstEntry = $entries[0]
$lastEntry  = $entries[-1]

$startTimeIso = $firstEntry.t.ToString('yyyy-MM-ddTHH:mm:ssZ')
$endTimeIso   = $lastEntry.t.ToString('yyyy-MM-ddTHH:mm:ssZ')
$durationMin  = [math]::Round(($lastEntry.t - $firstEntry.t).TotalMinutes, 1)
$totalFloors  = $lastEntry.floor
$actReached  = $lastEntry.act + 1   # jsonl is 0-indexed; record convention 1-indexed
$finalHp      = $lastEntry.hp
$finalGold    = $lastEntry.gold

# Detect halt context from last room type
# (real halt_reason still requires operator inspection -- victory vs death
# vs stall vs runcap can't always be inferred from jsonl alone)
$lastRoom        = $lastEntry.roomType
$inferredHalt    = 'death'  # safe default for trial-v1 (most runs end in death)
$inferredScreen  = 'Combat'
$inferredBoss    = $null
if ($lastRoom -eq 'Boss') {
    if ($actReached -eq 1) { $inferredBoss = 'act1_boss' }
    elseif ($actReached -eq 2) { $inferredBoss = 'act2_boss' }
    elseif ($actReached -eq 3) { $inferredBoss = 'act3_boss' }
}

# ----------------------------------------------------------------
# Parse trace.log for command_count + ipc_error_count
# trace.log lines start with ISO timestamp:
#   2026-05-05T14:33:23.0644111Z BridgeCommandReader dispatching id=3663 type=EndTurn
#   2026-05-05T14:33:24.1898317Z BridgeCommandReader wrote result id=3663 status=error
# Window: $startedDt .. $lastEntry.t + 5 minutes (cover post-floor cleanup)
# ----------------------------------------------------------------
$commandCount  = 0
$ipcErrorCount = 0
$stallCount    = 0  # placeholder; stalls are operator-detected, not in trace
if (-not $NoTraceCounts -and (Test-Path -LiteralPath $tracePath)) {
    $windowEnd = $lastEntry.t.AddMinutes(5)
    foreach ($line in Get-Content -LiteralPath $tracePath) {
        if ($line.Length -lt 28) { continue }
        $tsRaw = $line.Substring(0, 28)
        try {
            $tsUtc = ([datetime]::Parse($tsRaw)).ToUniversalTime()
        } catch {
            continue
        }
        if ($tsUtc -lt $startedDt) { continue }
        if ($tsUtc -gt $windowEnd) { break }
        if ($line -match 'BridgeCommandReader dispatching id=') {
            $commandCount++
        } elseif ($line -match 'wrote result id=\d+ status=error') {
            $ipcErrorCount++
        }
    }
} elseif ($NoTraceCounts) {
    Write-Host "[skip] trace.log scan (-NoTraceCounts)" -ForegroundColor DarkGray
} else {
    Write-Warning "trace.log not found at $tracePath; command_count and ipc_error_count left at 0"
}

# ----------------------------------------------------------------
# Knowledge condition mapping
# ----------------------------------------------------------------
$knowledgeCondition = switch ($prior) {
    'A0' { 'A0-zero-shot' }
    'B0' { 'B0-with-priors' }
    default { throw "Unknown prior value: $prior" }
}

# ----------------------------------------------------------------
# Build frontmatter + body
# ----------------------------------------------------------------
$bridgeVersion = 'v0.2.0'  # trial-v1 floor; finalize bumps if needed
$gameVersion   = '0.104.0'
$specVersion   = 'trial-v1'

# Provider lookup table (matches existing run-record convention)
$providerMap = @{
    'claude-opus-4.7'  = 'github-copilot'
    'gpt-5.5'          = 'openai'
    'glm-5.1'          = 'zai'
    'gemini-3.1-pro'   = 'google'
    'deepseek-v3.5'    = 'openrouter'
}
$provider = if ($providerMap.ContainsKey($model)) { $providerMap[$model] } else { '<provider>' }

$inferredBossLine = if ($inferredBoss) { $inferredBoss } else { 'null' }
$deathFloorLine   = if ($inferredHalt -eq 'death') { $totalFloors } else { 'null' }
$deathScreenLine  = if ($inferredHalt -eq 'death') { $inferredScreen } else { 'null' }

$frontmatter = @"
---
run_id: $wallRunId
spec_version: $specVersion
knowledge_condition: $knowledgeCondition
bridge_version: $bridgeVersion
game_version: $gameVersion
model: $model
model_provider: $provider
opencode_session_id: ses_REPLACE_ME
character: $character
ascension: 0
seed: $seed
start_time_utc: $startTimeIso
end_time_utc: $endTimeIso
duration_minutes: $durationMin
command_count: $commandCount
ipc_error_count: $ipcErrorCount
stall_count: $stallCount
halt_reason: $inferredHalt
death_floor: $deathFloorLine
death_screen: $deathScreenLine
death_cause: null
victory_floor: null
boss_reached: $inferredBossLine
final_hp: $finalHp
final_gold: $finalGold
tokens_in: null
tokens_out: null
tokens_cache_read: null
tokens_cache_write: null
tokens_reasoning: null
tokens_total: null
cost_usd: null
wall_seconds: null
step_finish_count: null
act_reached: $actReached
total_floors: $totalFloors
total_card_picks: null
total_card_skips: null
total_relics_picked: null
total_potions_used: null
total_potions_bought: null
total_damage_taken: null
total_gold_gained: null
total_gold_spent: null
total_gold_lost: null
total_hp_healed: null
elites_fought: null
rests_taken: null
shops_visited: null
events_visited: null
rest_choice_heal: null
rest_choice_smith: null
killed_by: null
was_abandoned: null
run_time_seconds: null
---

## Summary

<TODO: One paragraph describing what happened and why the run ended.>

## Bridge findings

<TODO: Per protocol-v1.md section "Bridge findings vs strategic findings".
 If none observed: write "None observed.">

## Decision log highlights

<TODO: 3-7 bullet points covering tough card-play forks, contested map
 choices, Neow choice, key event, key shop. One line each.>

## Notes for maintainers

<TODO: Optional. Anything actionable for the harness or this protocol.
 Omit the section entirely if there's nothing to add.>
"@

# ----------------------------------------------------------------
# Write file
# ----------------------------------------------------------------
$action = if (Test-Path -LiteralPath $recordPath) { 'Overwrite' } else { 'Create' }
if ($PSCmdlet.ShouldProcess($recordPath, $action)) {
    Set-Content -LiteralPath $recordPath -Value $frontmatter -Encoding utf8 -NoNewline
}

# ----------------------------------------------------------------
# Banner
# ----------------------------------------------------------------
$bar = '=' * 70
Write-Host ''
Write-Host $bar -ForegroundColor Cyan
Write-Host "  SCAFFOLDED  $wallRunId" -ForegroundColor Cyan
Write-Host $bar -ForegroundColor Cyan
Write-Host "  path:           $recordPath"
Write-Host "  schedule row:   $RunId  ($model / $character / $prior / k=$($row.k_index))"
Write-Host "  slot:           $Slot"
Write-Host "  start - end:    $startTimeIso - $endTimeIso"
Write-Host "  duration:       $durationMin min"
Write-Host "  floors / act:   $totalFloors floors, act $actReached, last room $lastRoom"
Write-Host "  HP / gold:      HP $finalHp, gold $finalGold"
Write-Host "  inferred halt:  $inferredHalt (verify against actual screen!)"
Write-Host "  bridge counts:  $commandCount commands, $ipcErrorCount ipc errors"
Write-Host ''
Write-Host '  next steps:' -ForegroundColor Yellow
Write-Host '    1. Open the record file and paste opencode_session_id (ses_...)'
Write-Host '    2. Verify halt_reason / death_cause / victory_floor by hand'
Write-Host '    3. Have the agent fill Summary + Bridge findings + Decision log'
Write-Host '    4. Run finalize-run.ps1 (or finalize-and-scaffold.ps1) to patch'
Write-Host '       tokens + .run-derived stats + regenerate runs.csv'
Write-Host ''
