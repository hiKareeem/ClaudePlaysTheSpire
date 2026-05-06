#requires -Version 5.1
<#
.SYNOPSIS
    Watch a slot's IPC dir for run termination and notify the operator.

.DESCRIPTION
    Polls <ipc>/state.json every -PollSeconds seconds. Captures the screen
    name observed at startup. When the screen transitions to MainMenu (and
    was not MainMenu at startup), emits a terminal notification:

      - BurntToast desktop toast if the BurntToast module is installed
      - Otherwise: 3 console beeps + ANSI-flashed banner in the host window

    After notifying, the watcher exits 0. The operator can re-launch it
    against the next run (idempotent).

    This watcher INTENTIONALLY does NOT detect stalls. trial-v0 showed that
    rate-limit pauses can be 5-30+ minutes and runs resume successfully
    afterwards. False stall alarms would train the operator to ignore real
    terminal alerts. Terminal-only is the design.

.PARAMETER Slot
    Single uppercase letter (A-Z) selecting the slot's IPC dir via env var
    SPIREBRIDGE_IPC_DIR_<Slot>. Falls back to %APPDATA%\SlayTheSpire2\hermesbridge.

.PARAMETER PollSeconds
    Polling interval. Default 10s.

.PARAMETER QuietExit
    If set, exits silently (no toast, no beeps) when terminal detected.
    Useful for piping or programmatic callers.

.EXAMPLE
    .\watch-runs.ps1 -Slot A

    Watches slot A; emits one toast + beep on terminal; exits.

.EXAMPLE
    .\watch-runs.ps1 -Slot B -PollSeconds 30

    Watches slot B with a 30-second poll interval (lighter on disk).

.NOTES
    Termination signal:
        screen.name == "MainMenu" AND startup screen != "MainMenu"

    The bridge writes screen=GameOver for ~3s before auto-dismissing to
    MainMenu, so we tolerate up to ~PollSeconds latency on detection. The
    GameOver screen itself is too brittle to poll for at 10s intervals.

    Death-vs-victory disambiguation is left to the operator (read the
    final jsonl line). Watcher's job is wake-up only.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[A-Z]$')]
    [string]$Slot,

    [ValidateRange(2, 600)]
    [int]$PollSeconds = 10,

    [switch]$QuietExit
)

$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Resolve IPC dir for this slot
# ---------------------------------------------------------------------------

function Get-IpcDir {
    param([string]$SlotLetter)

    $envName = "SPIREBRIDGE_IPC_DIR_$SlotLetter"
    $val = [Environment]::GetEnvironmentVariable($envName, 'Process')
    if (-not $val) { $val = [Environment]::GetEnvironmentVariable($envName, 'User') }
    if (-not $val) { $val = [Environment]::GetEnvironmentVariable($envName, 'Machine') }

    if ($val) {
        return $val
    }

    $fallback = Join-Path $env:APPDATA 'SlayTheSpire2\hermesbridge'
    Write-Warning "$envName not set. Falling back to default IPC dir: $fallback"
    return $fallback
}

# ---------------------------------------------------------------------------
# Read state.json safely (the bridge writes it from another process)
# ---------------------------------------------------------------------------

function Read-Screen {
    param([string]$StateJsonPath)

    if (-not (Test-Path -LiteralPath $StateJsonPath)) {
        return $null
    }

    # The bridge can be mid-write; retry briefly on parse failure.
    for ($i = 0; $i -lt 3; $i++) {
        try {
            $raw = Get-Content -LiteralPath $StateJsonPath -Raw -ErrorAction Stop
            if (-not $raw) { return $null }
            $obj = $raw | ConvertFrom-Json -ErrorAction Stop
            if ($obj.screen -and $obj.screen.name) {
                return [string]$obj.screen.name
            }
            return $null
        }
        catch {
            Start-Sleep -Milliseconds 200
        }
    }
    return $null
}

# ---------------------------------------------------------------------------
# Notification (BurntToast preferred; fallback beep + ANSI banner)
# ---------------------------------------------------------------------------

function Send-TerminalNotification {
    param(
        [string]$SlotLetter,
        [string]$LastScreen,
        [string]$IpcDir
    )

    if ($QuietExit) {
        Write-Host "Terminal detected (slot $SlotLetter). -QuietExit set; skipping notification."
        return
    }

    $title = "SpireBridge: Slot $SlotLetter run TERMINATED"
    $body  = "Last screen: $LastScreen. Run finalize-and-complete.ps1."

    # Try BurntToast first
    try {
        if (Get-Module -ListAvailable -Name BurntToast -ErrorAction SilentlyContinue) {
            Import-Module BurntToast -ErrorAction Stop
            New-BurntToastNotification -Text $title, $body
            Write-Host "[toast] $title" -ForegroundColor Green
            return
        }
    }
    catch {
        Write-Warning "BurntToast available but failed: $($_.Exception.Message). Falling back to beep."
    }

    # Fallback: 3 beeps + ANSI red banner
    for ($i = 0; $i -lt 3; $i++) {
        [console]::Beep(880, 200)
        Start-Sleep -Milliseconds 100
    }

    $bar = '=' * 70
    Write-Host ''
    Write-Host $bar -ForegroundColor Red
    Write-Host "  TERMINAL  -  $title" -ForegroundColor Red
    Write-Host "  $body" -ForegroundColor Yellow
    Write-Host "  IPC dir: $IpcDir" -ForegroundColor DarkGray
    Write-Host $bar -ForegroundColor Red
    Write-Host ''
}

# ---------------------------------------------------------------------------
# Main loop
# ---------------------------------------------------------------------------

$ipcDir = Get-IpcDir -SlotLetter $Slot
$stateJson = Join-Path $ipcDir 'state.json'

Write-Host "Watching slot $Slot" -ForegroundColor Cyan
Write-Host "  IPC dir   : $ipcDir"
Write-Host "  state.json: $stateJson"
Write-Host "  poll      : every $PollSeconds s"
Write-Host "  exits on  : screen transition to MainMenu (death/victory/quit)"
Write-Host "  Ctrl+C to abort."
Write-Host ''

# Capture startup screen
$startupScreen = Read-Screen -StateJsonPath $stateJson
if ($null -eq $startupScreen) {
    Write-Warning "Could not read startup screen from $stateJson. Treating startup as 'unknown'."
    $startupScreen = '<unknown>'
}
Write-Host "Startup screen: $startupScreen" -ForegroundColor DarkGray

if ($startupScreen -eq 'MainMenu') {
    Write-Warning "Startup screen is already MainMenu. Watcher will wait for it to leave MainMenu first, then return to MainMenu."
}

$lastScreen = $startupScreen
$everLeftMainMenu = ($startupScreen -ne 'MainMenu' -and $startupScreen -ne '<unknown>')
$tickCount = 0

try {
    while ($true) {
        Start-Sleep -Seconds $PollSeconds
        $tickCount++

        $current = Read-Screen -StateJsonPath $stateJson
        if ($null -eq $current) {
            # Transient: file gone or unparseable. Don't update lastScreen.
            if (($tickCount % 6) -eq 0) {
                Write-Host "[tick $tickCount] state.json unreadable - retrying" -ForegroundColor DarkYellow
            }
            continue
        }

        # Screen change logging
        if ($current -ne $lastScreen) {
            Write-Host "[tick $tickCount] screen: $lastScreen -> $current" -ForegroundColor DarkCyan
            $lastScreen = $current
            if ($current -ne 'MainMenu' -and $current -ne '<unknown>') {
                $everLeftMainMenu = $true
            }
        }
        elseif (($tickCount % 12) -eq 0) {
            # Heartbeat every 12 ticks (2 minutes at default poll)
            Write-Host "[tick $tickCount] still on $current" -ForegroundColor DarkGray
        }

        # Terminal condition
        if ($current -eq 'MainMenu' -and $everLeftMainMenu) {
            Send-TerminalNotification -SlotLetter $Slot -LastScreen $current -IpcDir $ipcDir
            exit 0
        }
    }
}
catch [System.Management.Automation.PipelineStoppedException] {
    Write-Host ''
    Write-Host "Watcher aborted by Ctrl+C." -ForegroundColor Yellow
    exit 130
}
