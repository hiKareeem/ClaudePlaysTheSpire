# autopilot-lib.ps1 → Python port scoping

Source: `autopilot-lib.ps1` (288 lines). Read pass complete.

## Module shape

The library has 5 logical sections, all small and well-isolated:

| Section | Lines | Purpose | Port complexity |
|---|---|---|---|
| Paths | ~30–37 | IPC dir resolution, env override `HERMES_IPC_DIR` | Trivial |
| Session state | ~40–87 | Persistent `nextId` / `cmdCount` across script invocations | Easy |
| Diagnostics | ~89–103 | `Log-Finding` / `Get-Findings` — yellow stderr + in-memory list | Easy |
| State I/O | ~105–149 | `Read-State` (retry loop), `Wait-Revision` (poll), `Clear-Ipc` | Easy |
| Command dispatch | ~151–226 | `Send-BridgeCommand` — write cmd, poll result, wait revision, return result+state+ok+stalled+id | Medium (timing-sensitive) |
| Session log | ~228–276 | `Write-SessionLog` — markdown report at end of run | Easy |

No external network calls. No complex state. No threading. Pure file IPC + JSON.

## Direct function-by-function port plan

Proposed Python module: `hermes_bridge.py` (or `tools/hermes_bridge/__init__.py` if we want a package).

| PowerShell | Python | Notes |
|---|---|---|
| `Read-State` | `read_state() -> dict` | Replace 50ms sleep loop with `time.sleep(0.05)`. Use `pathlib.Path` and `json.loads`. |
| `Wait-Revision -AfterRevision -TimeoutSec` | `wait_revision(after_revision: int, timeout_sec: int = 30) -> dict \| None` | Same semantics. Returns `None` on timeout. |
| `Clear-Ipc` | `clear_ipc()` | `pathlib.Path.unlink(missing_ok=True)`. |
| `Send-BridgeCommand` | `send_bridge_command(command: dict, result_timeout_sec: int = 10, revision_timeout_sec: int = 30) -> CommandResult` | Returns a dataclass. Uses 100ms poll like v0. Writes `commands.json` with `Path.write_text(..., encoding='utf-8', newline='')`. |
| `Log-Finding` | `log_finding(message: str)` | `print` to stderr in yellow (use `colorama` or just ANSI codes for cross-platform). |
| `Get-Findings` | `get_findings() -> list[str]` | Returns a copy, not a reference. |
| `Reset-Session -StartingId` | `reset_session(starting_id: int = 1)` | Same. |
| `Save-SessionState` | `_save_session_state()` | Internal. |
| `Get-IpcPaths` | `get_ipc_paths() -> IpcPaths` | Returns a dataclass. |
| `Write-SessionLog -Character -HaltReason -FinalState -TraceTailLines` | `write_session_log(character: str, halt_reason: str, final_state: dict, trace_tail_lines: int = 100) -> Path` | Same. |

Module-level state mirrors PS script-scope vars: `_NEXT_ID`, `_CMD_COUNT`, `_FINDINGS`, `_RUN_START`. Loaded from `autopilot-session.json` at import; `reset_session()` clears.

## Concrete recommendations

1. **Use a dataclass for `Send-BridgeCommand` return.** PowerShell's pscustomobject tolerates field drift; Python should pin it:

   ```python
   @dataclass
   class CommandResult:
       result: dict | None
       state: dict | None
       ok: bool
       stalled: bool
       id: int
   ```

2. **Use `pathlib.Path` everywhere.** Cross-platform out of the box. APPDATA on Windows resolves via `os.environ['APPDATA']`; on Linux/macOS the bridge runs only on Windows but the operator scripts may run on Linux for analysis — keep the IPC-dir resolver defensible.

3. **Keep the IPC-dir env override (`HERMES_IPC_DIR`).** Already used by multi-instance test setups; do not break compatibility.

4. **Keep the `autopilot-session.json` schema identical** so a Python session can resume a PowerShell session and vice versa during the transition. Field set: `{nextId, cmdCount}`. Same file path.

5. **Match exact PowerShell timeouts** (`ResultTimeoutSec=10`, `RevisionTimeoutSec=30`, `Read-State` 20×50ms retry). Behavioral parity is the test.

6. **Skip the `Write-Host` color theatre.** Python `log_finding` should write plain stderr. Color via `rich` or `colorama` is optional; not worth adding a dep for.

7. **Drop the strict-mode equivalent.** PowerShell `Set-StrictMode -Version Latest` has no Python analog beyond standard linting. `mypy --strict` covers the same surface.

## Test plan

Three layers, all runnable without StS2 running:

1. **Unit tests** (`tests/test_hermes_bridge_py.py`):
   - `read_state` retries until file appears.
   - `wait_revision` returns `None` on timeout, returns state on bump.
   - `send_bridge_command` writes correct JSON, polls result correctly, classifies ok/stalled.
   - `log_finding` accumulates; `get_findings` returns a copy.
   - Session-state round-trip: write `_NEXT_ID=42`, reimport module, observe 42.

2. **Behavioral parity tests** (`tests/test_powershell_python_parity.py`):
   - Spawn PS `Send-BridgeCommand` and Python `send_bridge_command` against a mock bridge (a small script that watches `commands.json` and writes `result.json`). Assert identical CommandResult shape.

3. **Live-bridge smoke test** (manual, run-once):
   - StartRun → 3 PlayCard → EndTurn → Proceed via Python lib. Compare `trace.log` output against a known-good PS run.

## File layout proposal

```
tools/
  hermes_bridge/                # New package
    __init__.py                 # Exports public surface
    paths.py                    # IpcPaths dataclass + resolver
    state_io.py                 # read_state, wait_revision, clear_ipc
    dispatch.py                 # send_bridge_command + CommandResult
    session.py                  # session state, reset_session, save/load
    findings.py                 # log_finding, get_findings
    session_log.py              # write_session_log
  spirebench-summary.py         # (existing, already Python)
  ...
tests/
  test_hermes_bridge_py.py      # Python unit tests
  test_hermes_bridge_parity.py  # PS-vs-Python behavioral tests
```

Single-file alternative: `tools/hermes_bridge.py` (one ~300-line module). Lower navigation cost, no `__init__` ceremony. Recommend single-file unless the API grows past ~500 lines.

## Effort estimate

- Single-file port: **~3–4 hours** including tests. Most of it is rote translation; the only judgment call is the dataclass shape for `CommandResult`.
- Multi-file package: **+1 hour** for layout and `__init__` plumbing. Not recommended for current scope.
- Behavioral parity test setup: **+2 hours**. Worth it before retiring the PS lib; defer if we plan to keep both running side-by-side initially.

Total recommended: **single-file port + unit tests + manual smoke = ~4–5 hours**. Parity tests deferred.

## Migration path

1. Write `tools/hermes_bridge.py` + `tests/test_hermes_bridge_py.py`. Don't touch `autopilot-lib.ps1`.
2. Manual smoke against a live bridge: 1 run, IRONCLAD, Python-only driver.
3. If smoke passes, ship `hermes_bridge.py` alongside `autopilot-lib.ps1`. Both work. Python is opt-in via a new `tools/run-python.ps1` thin wrapper or direct `python -m hermes_bridge`.
4. Once a public contributor uses the Python path successfully, deprecate the PS lib in v1.x or v2 docs. Keep the PS file in-tree until at least one full trial completes on Python.
5. Maintainer scripts (`tools/maintainer/*.ps1`) stay PowerShell — defer per Kareem's call (2026-05-04).

## Out of scope for this port

- `tools/maintainer/*.ps1` — release packaging, append-run-csv, parse-run-history, get-session-tokens, finalize-run. Operator-only; defer.
- `tools/*.ps1` (non-maintainer) — `read-state.ps1`, `list-cards.ps1`, etc. Small, self-contained, port if/when contributors ask.
- Bridge-side C# (`HermesBridgeCode/*.cs`) — pinned to Godot/Mono runtime; cannot be ported.

## Open question

- Cross-platform IPC dir on non-Windows: bridge only runs on Windows (StS2 modding limitation). If a Linux contributor ever runs the analysis pipeline, they'd point `HERMES_IPC_DIR` at a copied IPC dir or a network mount. The Python port should not assume `APPDATA` exists.

---

Source: `autopilot-lib.ps1` lines 1–288, `docs/benchmark/trial-v0-findings-audit.md` §5 and §9.
