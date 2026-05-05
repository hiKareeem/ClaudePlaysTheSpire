<#
.SYNOPSIS
    Pre-flight verification for SpireBench / SpireBridge benchmark sessions.

.DESCRIPTION
    Three independent checks, each can pass / warn / fail:

    1. modVersion match. The version baked into the loaded DLL (read from
       HermesBridge.json next to it, since AssemblyVersion is not strict
       in this project) matches the repo-HEAD HermesBridge.json. A
       mismatch means the operator forgot to copy the new build into the
       game's mods folder, or has an old shortcut pointing at a stale
       deployment. Either way the run would record under a misleading
       modVersion and silently corrupt the dataset -- this is the most
       common cross-session footgun and the reason this script exists.

    2. Minimum bridge version. The deployed build must be >= the
       MinBridgeVersion parameter (default 0.2.0). v0.2.0 is the
       protocol-v1 floor; older builds lack stateVersion / ForceRefresh /
       state_inconsistent and produce records that cannot be merged into
       the v1 dataset.

    3. IPC root match. The trace.log inside the active IPC root reports
       (via BridgePaths' startup diagnostic) which directory the bridge
       chose this session. We compare that against the operator-intended
       root passed via -ExpectedIpcRoot (or auto-derived from
       SPIREBRIDGE_IPC_DIR / HERMES_IPC_DIR / appdata fallback). A
       mismatch indicates a multi-instance cross-talk hazard and is a
       hard fail.

    The script EXITS NON-ZERO on any FAIL, ZERO on all-PASS, and ZERO
    with a warning summary if only WARN results occurred. Designed for
    invocation at the top of a benchmark run script:

        pwsh -File tools/preflight-dll-version.ps1 ; if ($LASTEXITCODE) { throw "preflight failed" }

.PARAMETER ModsRoot
    Directory containing the deployed HermesBridge folder. Defaults to
    the path used by Steam StS2 installs ($env:ProgramFiles(x86)\Steam\
    steamapps\common\Slay the Spire 2\mods). Override for non-Steam
    installs.

.PARAMETER ExpectedIpcRoot
    The IPC base directory the operator expects this session to use.
    If omitted, derived in the same precedence the bridge itself uses:
    SPIREBRIDGE_IPC_DIR -> HERMES_IPC_DIR -> default appdata path. The
    derived value is reported in the script output for transparency.

.PARAMETER MinBridgeVersion
    Minimum acceptable bridge version (semver). Default: 0.2.0 (the
    protocol-v1 floor).

.PARAMETER RepoRoot
    Repo checkout root. Default: parent directory of the script's own
    folder (i.e. tools/.. -> repo root). Used only to read repo-HEAD
    HermesBridge.json for the modVersion-match check; if the script is
    being run outside a checkout, pass -RepoRoot explicitly or skip
    that check via -SkipRepoMatch.

.PARAMETER SkipRepoMatch
    Skip check #1 (repo-HEAD modVersion match). Useful when running on
    a machine that doesn't have the source checkout (e.g. a dedicated
    benchmark runner that only has the deployed DLL).
#>
[CmdletBinding()]
param(
    [string] $ModsRoot,
    [string] $ExpectedIpcRoot,
    [string] $MinBridgeVersion = '0.2.0',
    [string] $RepoRoot,
    [switch] $SkipRepoMatch
)

$ErrorActionPreference = 'Stop'

# Defer $PSScriptRoot-derived defaults to script body. In Windows PowerShell
# 5.1 the automatic variable is not bound during param() default evaluation,
# which made the original `[string] $RepoRoot = (Split-Path $PSScriptRoot
# -Parent)` form throw "empty string" when invoked via `-File`.
if (-not $RepoRoot) { $RepoRoot = (Split-Path $PSScriptRoot -Parent) }

# Result accumulator. Each entry: { Check, Status (PASS|WARN|FAIL), Detail }.
$results = New-Object System.Collections.Generic.List[psobject]
function Record($check, $status, $detail) {
    $results.Add([pscustomobject]@{ Check = $check; Status = $status; Detail = $detail })
}

# Version extraction: HermesBridge.json carries either "version" or
# "modVersion" depending on era, and historic values include a "v"
# prefix (e.g. "v0.2.0") that we want to strip for clean comparison
# and display. Returns the bare semver string ("0.2.0") or $null.
function Get-ManifestVersion([object] $manifest) {
    if (-not $manifest) { return $null }
    $v = $manifest.modVersion
    if (-not $v) { $v = $manifest.version }
    if (-not $v) { return $null }
    return ($v -replace '^[vV]', '')
}

# --- Resolve the deployed HermesBridge folder ----------------------------
if (-not $ModsRoot) {
    # Conventional Steam install path. We don't enumerate Steam libraries
    # here because the operator can always override; keeping this simple
    # avoids a registry probe that fails on non-Steam machines.
    $ModsRoot = Join-Path ${env:ProgramFiles(x86)} 'Steam\steamapps\common\Slay the Spire 2\mods'
}
$deployedDir = Join-Path $ModsRoot 'HermesBridge'
$deployedDll = Join-Path $deployedDir 'HermesBridge.dll'
$deployedManifest = Join-Path $deployedDir 'HermesBridge.json'

if (-not (Test-Path $deployedDll)) {
    Record 'deployment' 'FAIL' "HermesBridge.dll not found at $deployedDll. Set -ModsRoot to the correct StS2 mods directory."
}
elseif (-not (Test-Path $deployedManifest)) {
    Record 'deployment' 'FAIL' "HermesBridge.json not found at $deployedManifest (DLL present but manifest missing -- incomplete deployment)."
}
else {
    Record 'deployment' 'PASS' "DLL + manifest present at $deployedDir"
}

# --- Read deployed manifest version --------------------------------------
$deployedVersion = $null
if (Test-Path $deployedManifest) {
    try {
        $deployedManifestObj = Get-Content $deployedManifest -Raw | ConvertFrom-Json
        $deployedVersion = Get-ManifestVersion $deployedManifestObj
    } catch {
        Record 'manifest-parse' 'FAIL' "Could not parse $deployedManifest as JSON: $($_.Exception.Message)"
    }
}

# --- Check 1: modVersion match against repo HEAD --------------------------
if ($SkipRepoMatch) {
    Record 'repo-match' 'WARN' 'Skipped (-SkipRepoMatch).'
}
elseif (-not $deployedVersion) {
    Record 'repo-match' 'FAIL' 'Deployed manifest version unreadable; cannot compare against repo HEAD.'
}
else {
    $repoManifest = Join-Path $RepoRoot 'HermesBridge.json'
    if (-not (Test-Path $repoManifest)) {
        Record 'repo-match' 'WARN' "Repo manifest not found at $repoManifest. Pass -RepoRoot or -SkipRepoMatch."
    }
    else {
        try {
            $repoVersion = Get-ManifestVersion (Get-Content $repoManifest -Raw | ConvertFrom-Json)
            if (-not $repoVersion) {
                Record 'repo-match' 'WARN' "Repo manifest has no version/modVersion field at $repoManifest."
            }
            elseif ($repoVersion -eq $deployedVersion) {
                Record 'repo-match' 'PASS' "Deployed v$deployedVersion matches repo HEAD"
            } else {
                Record 'repo-match' 'FAIL' "Deployed v$deployedVersion does NOT match repo HEAD v$repoVersion. Rebuild and copy HermesBridge.dll + HermesBridge.json into $deployedDir before starting a session, or this run will record under a stale modVersion."
            }
        } catch {
            Record 'repo-match' 'WARN' "Could not parse repo manifest at ${repoManifest}: $($_.Exception.Message)"
        }
    }
}

# --- Check 2: minimum bridge version --------------------------------------
function Compare-SemVer {
    param([string] $a, [string] $b)
    # Returns -1 / 0 / 1. Strips any "-suffix" pre-release tag before
    # comparing; for the floor check, "0.2.0-rc1" is treated as 0.2.0
    # (good enough -- pre-release builds are operator-internal anyway).
    function Parts([string] $v) {
        $clean = ($v -split '-')[0]
        return @($clean -split '\.' | ForEach-Object {
            $n = 0; if ([int]::TryParse($_, [ref]$n)) { $n } else { 0 }
        })
    }
    $pa = Parts $a; $pb = Parts $b
    $len = [Math]::Max($pa.Count, $pb.Count)
    for ($i = 0; $i -lt $len; $i++) {
        $ai = if ($i -lt $pa.Count) { $pa[$i] } else { 0 }
        $bi = if ($i -lt $pb.Count) { $pb[$i] } else { 0 }
        if ($ai -gt $bi) { return 1 }
        if ($ai -lt $bi) { return -1 }
    }
    return 0
}

if (-not $deployedVersion) {
    Record 'min-version' 'FAIL' "Cannot check min version -- deployed manifest version unreadable."
}
elseif ((Compare-SemVer $deployedVersion $MinBridgeVersion) -lt 0) {
    Record 'min-version' 'FAIL' "Deployed v$deployedVersion is below minimum v$MinBridgeVersion. Update the bridge before starting a v1 benchmark session."
}
else {
    Record 'min-version' 'PASS' "Deployed v$deployedVersion >= v$MinBridgeVersion"
}

# --- Check 3: IPC root match against trace.log ----------------------------
# Mirror BridgePaths.ResolveBaseDirectory's precedence so the expected
# root we reason about is the same one the bridge would have chosen.
function Resolve-ExpectedIpcRoot {
    if ($env:SPIREBRIDGE_IPC_DIR) {
        return @{ Path = $env:SPIREBRIDGE_IPC_DIR; Source = 'SPIREBRIDGE_IPC_DIR' }
    }
    if ($env:HERMES_IPC_DIR) {
        return @{ Path = $env:HERMES_IPC_DIR; Source = 'HERMES_IPC_DIR (deprecated)' }
    }
    # Default fallback. We deliberately don't try to read hermes-instance.cfg
    # here -- that's a deployment-side detail and the operator can override
    # via -ExpectedIpcRoot if they're using an instance config.
    $appdata = [Environment]::GetFolderPath('ApplicationData')
    return @{ Path = (Join-Path $appdata 'SlayTheSpire2\hermesbridge'); Source = 'default (appdata fallback)' }
}

$expected = if ($ExpectedIpcRoot) {
    @{ Path = $ExpectedIpcRoot; Source = '-ExpectedIpcRoot parameter' }
} else {
    Resolve-ExpectedIpcRoot
}

$traceLog = Join-Path $expected.Path 'trace.log'
if (-not (Test-Path $traceLog)) {
    # Not necessarily a fail -- bridge may not have run yet against this root.
    # But it does mean we cannot verify the match, so flag it.
    Record 'ipc-root' 'WARN' "trace.log not present at $traceLog (source: $($expected.Source)). Bridge may not have run against this root yet; cannot verify."
}
else {
    # Look for the most recent BridgePaths startup diagnostic. The line
    # format (from BridgePaths.ResolveBaseDirectory):
    #   "BridgePaths: SPIREBRIDGE_IPC_DIR override active: <path>"
    #   "BridgePaths: HERMES_IPC_DIR override active: <path> (DEPRECATED ...)"
    #   "BridgePaths: instance config '<id>' active: <path>"
    # Default fallback emits NO diagnostic (BridgePaths.cs comment + test
    # case 6 enforce this), so absence-of-line on a default-path session
    # is correct, and we must not fail the check just because no line is
    # found -- instead, infer that the active root is the default and
    # compare that against expected.Path directly.
    try {
        $diagLines = Select-String -Path $traceLog -Pattern 'BridgePaths: .* active: ' -SimpleMatch:$false -ErrorAction Stop
    } catch {
        $diagLines = @()
    }

    if ($diagLines.Count -eq 0) {
        # No diagnostic ever logged in this trace.log → bridge resolved to
        # default appdata path. Verify our expected root is also that
        # default; if so PASS, if not FAIL.
        $defaultPath = Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'SlayTheSpire2\hermesbridge'
        if ((Resolve-Path $expected.Path -ErrorAction SilentlyContinue).Path -eq (Resolve-Path $defaultPath -ErrorAction SilentlyContinue).Path) {
            Record 'ipc-root' 'PASS' "Active root = expected (both = default appdata path: $defaultPath)"
        } else {
            Record 'ipc-root' 'FAIL' "trace.log at $traceLog has no BridgePaths diagnostic, implying bridge resolved to default '$defaultPath', but expected root is '$($expected.Path)' (source: $($expected.Source)). Multi-instance cross-talk hazard."
        }
    }
    else {
        # Take the LAST matching line -- startup diagnostic appears once
        # per bridge load, but there may be several in a long-lived
        # trace.log across game restarts. The most recent one is what
        # this session is using.
        $lastDiag = $diagLines[-1].Line
        # Extract everything after "active: " up to end-of-line or first " (".
        if ($lastDiag -match 'active:\s+(.+?)(\s+\(|\s*$)') {
            $activeRoot = $matches[1].Trim()
            $resolvedActive = (Resolve-Path $activeRoot -ErrorAction SilentlyContinue).Path
            $resolvedExpected = (Resolve-Path $expected.Path -ErrorAction SilentlyContinue).Path
            if ($resolvedActive -and $resolvedExpected -and $resolvedActive -eq $resolvedExpected) {
                Record 'ipc-root' 'PASS' "Active root '$activeRoot' matches expected (source: $($expected.Source))"
            }
            elseif (-not $resolvedActive) {
                Record 'ipc-root' 'WARN' "Could not resolve active root '$activeRoot' from trace.log (path may not exist on this machine)."
            }
            else {
                Record 'ipc-root' 'FAIL' "Active root '$activeRoot' (per trace.log) does NOT match expected '$($expected.Path)' (source: $($expected.Source)). Multi-instance cross-talk hazard."
            }
        }
        else {
            Record 'ipc-root' 'WARN' "Could not parse BridgePaths diagnostic line: $lastDiag"
        }
    }
}

# --- Render report --------------------------------------------------------
"`nSpireBridge preflight"
"---------------------"
"  Deployed dir   : $deployedDir"
"  Deployed ver   : $(if ($deployedVersion) { 'v' + $deployedVersion } else { '<unreadable>' })"
"  Min version    : v$MinBridgeVersion"
"  Expected IPC   : $($expected.Path)"
"  IPC source     : $($expected.Source)"
""

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
