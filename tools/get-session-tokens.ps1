# tools/get-session-tokens.ps1
# ---------------------------------------------------------------
# Extract aggregated token usage for an OpenCode session, for use
# in SpireBench trial-v0 run-record YAML front-matter.
#
# Usage:
#   .\tools\get-session-tokens.ps1 -SessionId ses_245781889ffeWpFXFej2xZ5IKo
#
# Reads:
#   ~\.local\share\opencode\opencode.db  (SQLite, WAL mode)
#
# Output: YAML-ready key-value pairs, e.g.
#   tokens_in: 1234567
#   tokens_out: 45678
#   tokens_cache_read: 9876543
#   tokens_cache_write: 12345
#
# This is a passive read; it does NOT mutate the session DB. If the
# DB is locked by an active OpenCode process, the read may briefly
# block; that's acceptable for post-run reporting.
# ---------------------------------------------------------------

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SessionId,

    [string]$DbPath = (Join-Path $env:USERPROFILE ".local\share\opencode\opencode.db")
)

if (-not (Test-Path $DbPath)) {
    Write-Error "OpenCode session DB not found at: $DbPath"
    exit 1
}

# Load System.Data.SQLite if available; fall back to sqlite3.exe on PATH.
$useExe = $false
try {
    Add-Type -AssemblyName "System.Data.SQLite" -ErrorAction Stop
} catch {
    if (-not (Get-Command sqlite3.exe -ErrorAction SilentlyContinue)) {
        Write-Error "Need either System.Data.SQLite assembly OR sqlite3.exe on PATH. Install via 'winget install SQLite.SQLite' or similar."
        exit 1
    }
    $useExe = $true
}

# Schema note: OpenCode stores session parts in a `part` table; each row's
# `data` column is JSON. For step-finish parts, `data.tokens` has
# {input, output, cacheRead, cacheWrite}. We aggregate across the session.
#
# This query assumes a `part` table with columns (session_id, type, data).
# If your OpenCode build differs, run:  sqlite3 $DbPath ".schema"
# and adjust accordingly.

$sql = @"
SELECT data FROM part
 WHERE session_id = '$SessionId'
   AND type = 'step-finish';
"@

$rows = @()

if ($useExe) {
    $rows = & sqlite3.exe -readonly $DbPath $sql 2>$null
} else {
    $cs = "Data Source=$DbPath;Version=3;Read Only=True;"
    $conn = New-Object System.Data.SQLite.SQLiteConnection($cs)
    $conn.Open()
    try {
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $sql
        $reader = $cmd.ExecuteReader()
        while ($reader.Read()) {
            $rows += $reader.GetString(0)
        }
        $reader.Close()
    } finally {
        $conn.Close()
    }
}

if ($rows.Count -eq 0) {
    Write-Warning "No step-finish parts found for session_id=$SessionId. Either the session has no completed steps yet, or the schema differs (try: sqlite3 `"$DbPath`" .schema)."
    @"
tokens_in: null
tokens_out: null
tokens_cache_read: null
tokens_cache_write: null
"@
    exit 0
}

$tIn = 0L; $tOut = 0L; $tCacheR = 0L; $tCacheW = 0L

foreach ($json in $rows) {
    try {
        $obj = $json | ConvertFrom-Json -ErrorAction Stop
        if ($obj.tokens) {
            if ($null -ne $obj.tokens.input)      { $tIn      += [long]$obj.tokens.input }
            if ($null -ne $obj.tokens.output)     { $tOut     += [long]$obj.tokens.output }
            if ($null -ne $obj.tokens.cacheRead)  { $tCacheR  += [long]$obj.tokens.cacheRead }
            if ($null -ne $obj.tokens.cacheWrite) { $tCacheW  += [long]$obj.tokens.cacheWrite }
        }
    } catch {
        # Skip malformed rows silently; trace for the curious.
        Write-Verbose "Skipping unparseable part: $($_.Exception.Message)"
    }
}

@"
tokens_in: $tIn
tokens_out: $tOut
tokens_cache_read: $tCacheR
tokens_cache_write: $tCacheW
"@
