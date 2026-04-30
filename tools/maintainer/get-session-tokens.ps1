# tools/maintainer/get-session-tokens.ps1
# ---------------------------------------------------------------
# Extract aggregated token usage + wall-clock duration for an
# OpenCode session, for use in SpireBench trial-v0 run-record
# YAML front-matter.
#
# Usage:
#   .\tools\maintainer\get-session-tokens.ps1 -SessionId ses_245781889ffeWpFXFej2xZ5IKo
#
# Reads:
#   ~\.local\share\opencode\opencode.db  (SQLite, WAL mode)
#
# Output: YAML-ready key-value pairs, e.g.
#   tokens_in: 1234
#   tokens_out: 45678
#   tokens_cache_read: 9876543
#   tokens_cache_write: 12345
#   tokens_reasoning: 0
#   tokens_total: 11000000
#   cost_usd: 0.4231
#   wall_seconds: 1842
#   step_finish_count: 47
#
# Implementation note: shells out to `python` because neither
# sqlite3.exe nor System.Data.SQLite is reliably available on
# a fresh Windows install, but Python 3 ships sqlite3 in stdlib.
#
# This is a passive read; it does NOT mutate the session DB. Read
# uses ?mode=ro URI so a live OpenCode process can hold the WAL.
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
if (-not (Get-Command python -ErrorAction SilentlyContinue)) {
    Write-Error "python not on PATH; required for sqlite3 stdlib access."
    exit 1
}

$py = @'
import sqlite3, json, sys, os
db, sid = sys.argv[1], sys.argv[2]
con = sqlite3.connect(f"file:{db}?mode=ro", uri=True)
cur = con.cursor()
cur.execute("SELECT data, time_created, time_updated FROM part WHERE session_id = ?", (sid,))
t_in = t_out = t_cr = t_cw = t_reason = t_total = 0
cost = 0.0
sf_count = 0
t_first = None
t_last = None
for raw, tc, tu in cur.fetchall():
    if t_first is None or tc < t_first: t_first = tc
    if t_last  is None or tu > t_last:  t_last  = tu
    try:
        obj = json.loads(raw)
    except Exception:
        continue
    if obj.get("type") != "step-finish":
        continue
    sf_count += 1
    tk = obj.get("tokens") or {}
    t_in     += int(tk.get("input")     or 0)
    t_out    += int(tk.get("output")    or 0)
    t_reason += int(tk.get("reasoning") or 0)
    t_total  += int(tk.get("total")     or 0)
    cache = tk.get("cache") or {}
    t_cr += int(cache.get("read")  or 0)
    t_cw += int(cache.get("write") or 0)
    c = obj.get("cost")
    if isinstance(c, (int, float)):
        cost += float(c)
if sf_count == 0 and t_first is None:
    print("# no parts found for session_id", sid, file=sys.stderr)
    print("tokens_in: null")
    print("tokens_out: null")
    print("tokens_cache_read: null")
    print("tokens_cache_write: null")
    print("tokens_reasoning: null")
    print("tokens_total: null")
    print("cost_usd: null")
    print("wall_seconds: null")
    print("step_finish_count: 0")
    sys.exit(0)
wall = round((t_last - t_first) / 1000.0) if (t_first and t_last) else 0
print(f"tokens_in: {t_in}")
print(f"tokens_out: {t_out}")
print(f"tokens_cache_read: {t_cr}")
print(f"tokens_cache_write: {t_cw}")
print(f"tokens_reasoning: {t_reason}")
print(f"tokens_total: {t_total}")
print(f"cost_usd: {cost:.4f}")
print(f"wall_seconds: {wall}")
print(f"step_finish_count: {sf_count}")
'@

# Pipe the python source via stdin so we don't have to manage a temp file.
$py | python - $DbPath $SessionId
