"""SpireBridge IPC helper library — Python port of autopilot-lib.ps1.

This is a LIBRARY, not a runner. It defines primitives for an agent
(human or LLM) to drive the bridge tick-by-tick. It contains NO
decision logic, NO loops, and NO screen dispatch.

Usage:

    from spire_bridge import (
        read_state, send_bridge_command, write_session_log, reset_session,
    )

    state = read_state()
    res = send_bridge_command({"type": "StartRun", "character": "NECROBINDER"})
    state = res.state
    # ...decide...
    res = send_bridge_command({"type": "PlayCard", "handIndex": 0})
    # ...
    write_session_log(
        character="NECROBINDER",
        halt_reason="DEATH floor=11",
        final_state=res.state,
    )

Behavioral parity with autopilot-lib.ps1 v0.1.5:
  * Read-State retry: 20 attempts, 50ms apart.
  * Wait-Revision poll: 150ms; default 30s timeout.
  * Send-BridgeCommand result poll: 100ms; default 10s result timeout,
    30s revision timeout.
  * Session state file `autopilot-session.json` lives inside the IPC
    dir so multi-instance setups isolate automatically.

Env-var policy:
  * SPIREBRIDGE_IPC_DIR is canonical.
  * HERMES_IPC_DIR is a deprecated alias; if set without
    SPIREBRIDGE_IPC_DIR, a one-line stderr deprecation warning is
    emitted on first resolve.
  * Both unset: default APPDATA path
    `%APPDATA%/SlayTheSpire2/hermesbridge`.

Requires Python 3.10+ (uses PEP 604 union syntax).
"""

from __future__ import annotations

import json
import os
import sys
import time
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

__all__ = [
    "CommandResult",
    "IpcPaths",
    "clear_ipc",
    "get_findings",
    "get_ipc_paths",
    "log_finding",
    "read_state",
    "reset_session",
    "send_bridge_command",
    "wait_revision",
    "write_session_log",
]


# ---------------- Paths ----------------

def _resolve_ipc_dir() -> Path:
    """Resolve the IPC directory.

    Precedence:
      1. SPIREBRIDGE_IPC_DIR (canonical).
      2. HERMES_IPC_DIR (deprecated alias; warns once on stderr).
      3. %APPDATA%/SlayTheSpire2/hermesbridge default.

    The deprecation warning fires only on the first resolve in a process.
    Without APPDATA set (e.g. on Linux for analysis-only workflows) and
    no env-var override, raises FileNotFoundError so callers fail loudly
    rather than silently using `./SlayTheSpire2/hermesbridge`.
    """
    spire = os.environ.get("SPIREBRIDGE_IPC_DIR")
    if spire:
        return Path(spire)

    hermes = os.environ.get("HERMES_IPC_DIR")
    if hermes:
        if not _resolve_ipc_dir._warned:  # type: ignore[attr-defined]
            print(
                "spire_bridge: HERMES_IPC_DIR is deprecated; "
                "use SPIREBRIDGE_IPC_DIR instead",
                file=sys.stderr,
            )
            _resolve_ipc_dir._warned = True  # type: ignore[attr-defined]
        return Path(hermes)

    appdata = os.environ.get("APPDATA")
    if not appdata:
        raise FileNotFoundError(
            "Cannot resolve IPC dir: neither SPIREBRIDGE_IPC_DIR, "
            "HERMES_IPC_DIR, nor APPDATA is set. "
            "Set SPIREBRIDGE_IPC_DIR explicitly."
        )
    return Path(appdata) / "SlayTheSpire2" / "hermesbridge"


_resolve_ipc_dir._warned = False  # type: ignore[attr-defined]


@dataclass(frozen=True)
class IpcPaths:
    """Resolved bridge IPC file locations."""

    ipc_dir: Path
    state_file: Path
    cmds_file: Path
    res_file: Path
    trace_file: Path


def get_ipc_paths() -> IpcPaths:
    """Return the resolved IPC paths for the current environment.

    Resolves on every call so env-var changes mid-process take effect.
    """
    ipc_dir = _resolve_ipc_dir()
    return IpcPaths(
        ipc_dir=ipc_dir,
        state_file=ipc_dir / "state.json",
        cmds_file=ipc_dir / "commands.json",
        res_file=ipc_dir / "result.json",
        trace_file=ipc_dir / "trace.log",
    )


def _session_file() -> Path:
    return _resolve_ipc_dir() / "autopilot-session.json"


# ---------------- Session state ----------------

# Module-level state mirrors the PS script-scope vars.
_NEXT_ID: int = 1
_CMD_COUNT: int = 0
_FINDINGS: list[str] = []
_RUN_START: datetime = datetime.now()


def _load_session_state() -> None:
    """Load nextId / cmdCount from the on-disk session file, if present.

    Errors are swallowed (matching PS behavior); a corrupt session file
    just resets to defaults rather than failing the import.
    """
    global _NEXT_ID, _CMD_COUNT
    try:
        sess_path = _session_file()
    except FileNotFoundError:
        return
    if not sess_path.is_file():
        return
    try:
        sess = json.loads(sess_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return
    next_id = sess.get("nextId")
    if isinstance(next_id, int) and next_id > 1:
        _NEXT_ID = max(_NEXT_ID, next_id)
    cmd_count = sess.get("cmdCount")
    if isinstance(cmd_count, int):
        _CMD_COUNT = cmd_count


def _save_session_state() -> None:
    """Persist nextId / cmdCount to the IPC dir.

    Best-effort: failure is silent (matching PS), since the next command
    will retry. The IPC dir is created if missing.
    """
    try:
        path = _session_file()
    except FileNotFoundError:
        return
    try:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(
            json.dumps({"nextId": _NEXT_ID, "cmdCount": _CMD_COUNT}),
            encoding="utf-8",
        )
    except OSError:
        pass


def reset_session(starting_id: int = 1) -> None:
    """Reset per-run session counters. Call at the start of a fresh run.

    Pass `starting_id` to keep ids monotonic when resuming mid-session
    (e.g. after a manual command run).
    """
    global _NEXT_ID, _CMD_COUNT, _FINDINGS, _RUN_START
    _NEXT_ID = max(starting_id, _NEXT_ID)
    _CMD_COUNT = 0
    _FINDINGS = []
    _RUN_START = datetime.now()
    _save_session_state()


_load_session_state()


# ---------------- Diagnostics ----------------

def log_finding(message: str) -> None:
    """Record a stability finding for later inclusion in the session log.

    Writes to stderr and appends to the in-memory findings list.
    """
    ts = datetime.now().strftime("%H:%M:%S")
    line = f"[{ts}] {message}"
    print(f"  ! {line}", file=sys.stderr)
    _FINDINGS.append(line)


def get_findings() -> list[str]:
    """Return a copy of the findings list (callers cannot mutate state)."""
    return list(_FINDINGS)


# ---------------- State I/O ----------------

def read_state() -> dict[str, Any]:
    """Read state.json with retry (the bridge may be mid-write).

    Returns the parsed object. Raises RuntimeError on persistent failure.
    Matches PS Read-State: 20 attempts, 50ms apart.
    """
    paths = get_ipc_paths()
    last_err: Exception | None = None
    for _ in range(20):
        try:
            return json.loads(paths.state_file.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError) as exc:
            last_err = exc
            time.sleep(0.05)
    raise RuntimeError(
        f"Could not read {paths.state_file}: {last_err}"
    )


def wait_revision(
    after_revision: int,
    timeout_sec: int = 30,
) -> dict[str, Any] | None:
    """Block until state.json revision exceeds `after_revision`, or timeout.

    Returns the fresh state dict, or None on timeout. Matches PS
    Wait-Revision: 150ms poll interval.
    """
    deadline = time.monotonic() + timeout_sec
    while time.monotonic() < deadline:
        try:
            state = read_state()
            if int(state.get("revision", -1)) > after_revision:
                return state
        except RuntimeError:
            pass
        time.sleep(0.15)
    return None


def clear_ipc() -> None:
    """Delete stale commands.json and result.json.

    Call once at session start so a stale command from a previous crash
    doesn't double-apply on game boot.
    """
    paths = get_ipc_paths()
    for path in (paths.cmds_file, paths.res_file):
        try:
            path.unlink(missing_ok=True)
        except OSError:
            pass


# ---------------- Command dispatch ----------------

@dataclass(frozen=True)
class CommandResult:
    """Outcome of a Send-BridgeCommand call.

    Attributes:
      result: parsed result.json, or None on IPC timeout.
      state: state dict after the command, or None if unreadable.
      ok: True iff result.status == 'ok' AND no stall.
      stalled: True iff revision did not advance within the timeout.
      id: the command id that was used.
    """

    result: dict[str, Any] | None
    state: dict[str, Any] | None
    ok: bool
    stalled: bool
    id: int


def send_bridge_command(
    command: dict[str, Any],
    result_timeout_sec: int = 10,
    revision_timeout_sec: int = 30,
) -> CommandResult:
    """Write a single command, wait for its result, wait for a state revision bump.

    Does not raise on bridge-level errors; the caller inspects .ok.
    Automatically logs IPC_TIMEOUT, CMD_ERROR, STALL findings via
    log_finding().

    Behavioral parity with PS Send-BridgeCommand:
      * 100ms poll interval for result.json.
      * commands.json is written without a trailing newline (the bridge
        parses with strict JSON; trailing whitespace is tolerated but the
        PS script suppressed it via -NoNewline, so we match for byte-
        identical output).
    """
    global _NEXT_ID, _CMD_COUNT

    _CMD_COUNT += 1
    cmd_id = _NEXT_ID
    _NEXT_ID += 1
    _save_session_state()

    pre_state: dict[str, Any] | None = None
    try:
        pre_state = read_state()
    except RuntimeError:
        pass
    pre_rev = int(pre_state.get("revision", -1)) if pre_state else -1

    paths = get_ipc_paths()
    paths.ipc_dir.mkdir(parents=True, exist_ok=True)

    try:
        paths.res_file.unlink(missing_ok=True)
    except OSError:
        pass

    payload = json.dumps(
        {"id": cmd_id, "command": command},
        separators=(",", ":"),
    )
    paths.cmds_file.write_text(payload, encoding="utf-8")

    cmd_type = command.get("type", "?")

    # Wait for matching result.json
    deadline = time.monotonic() + result_timeout_sec
    result: dict[str, Any] | None = None
    while time.monotonic() < deadline:
        if paths.res_file.is_file():
            try:
                r = json.loads(paths.res_file.read_text(encoding="utf-8"))
                if int(r.get("id", -1)) == cmd_id:
                    result = r
                    break
            except (OSError, json.JSONDecodeError):
                pass
        time.sleep(0.1)

    if result is None:
        log_finding(
            f"IPC_TIMEOUT id={cmd_id} cmd={cmd_type} "
            f"(no result after {result_timeout_sec}s)"
        )
        s: dict[str, Any] | None = None
        try:
            s = read_state()
        except RuntimeError:
            pass
        return CommandResult(
            result=None, state=s, ok=False, stalled=False, id=cmd_id,
        )

    if result.get("status") != "ok":
        log_finding(
            f"CMD_ERROR id={cmd_id} cmd={cmd_type} "
            f"msg='{result.get('message', '')}'"
        )

    state = wait_revision(
        after_revision=pre_rev,
        timeout_sec=revision_timeout_sec,
    )
    stalled = False
    if state is None:
        stalled = True
        try:
            state = read_state()
        except RuntimeError:
            state = None
        scr = "?"
        if state and isinstance(state.get("screen"), dict):
            scr = state["screen"].get("name", "?")
        log_finding(
            f"STALL id={cmd_id} cmd={cmd_type} screen={scr} "
            f"preRev={pre_rev} (no revision bump in {revision_timeout_sec}s)"
        )

    ok = (result.get("status") == "ok") and (not stalled)
    return CommandResult(
        result=result, state=state, ok=ok, stalled=stalled, id=cmd_id,
    )


# ---------------- Session log ----------------

def _repo_root() -> Path:
    """Repo root for the docs/ output dir.

    Matches the PS behavior: `$PSScriptRoot` was the `tools/` dir's
    parent (the script lived at repo root). The Python module lives at
    `tools/spire_bridge.py`, so the repo root is the parent of this
    file's parent.
    """
    return Path(__file__).resolve().parent.parent


def write_session_log(
    character: str,
    halt_reason: str,
    final_state: dict[str, Any] | None,
    trace_tail_lines: int = 100,
) -> Path:
    """Append a per-run markdown section to docs/autopilot-session-<date>.md.

    Call once per run, after halt. Returns the log file path.
    """
    log_dir = _repo_root() / "docs"
    log_dir.mkdir(parents=True, exist_ok=True)
    log_file = log_dir / f"autopilot-session-{datetime.now():%Y-%m-%d}.md"

    start_utc = (
        _RUN_START.astimezone(timezone.utc).strftime("%Y-%m-%dT%H:%MZ")
    )

    floor: int = -1
    hp = "n/a"
    last_scr = "?"
    if final_state and isinstance(final_state.get("run"), dict):
        run = final_state["run"]
        floor = int(run.get("totalFloor", -1))
        hp = f"{run.get('currentHp', '?')}/{run.get('maxHp', '?')}"
    if final_state and isinstance(final_state.get("screen"), dict):
        last_scr = final_state["screen"].get("name", "?")

    duration_min = (datetime.now() - _RUN_START).total_seconds() / 60.0

    lines: list[str] = []
    lines.append(f"## Run {start_utc} - {character}")
    lines.append("")
    lines.append(f"- Halt reason: {halt_reason}")
    lines.append(f"- End floor: {floor}, hp {hp}, last screen {last_scr}")
    lines.append(
        f"- Duration: {duration_min:.1f} min, {_CMD_COUNT} commands, "
        f"last id {_NEXT_ID - 1}"
    )
    lines.append("")
    lines.append("### Stability findings")
    if not _FINDINGS:
        lines.append("- None.")
    else:
        for f_ in _FINDINGS:
            lines.append(f"- {f_}")
    lines.append("")
    lines.append(f"### trace.log tail (last {trace_tail_lines} lines)")
    lines.append("```")
    paths = get_ipc_paths()
    if paths.trace_file.is_file():
        try:
            with paths.trace_file.open(encoding="utf-8", errors="replace") as fh:
                tail: list[str] = []
                for raw in fh:
                    tail.append(raw.rstrip("\n"))
                    if len(tail) > trace_tail_lines:
                        tail.pop(0)
            lines.extend(tail)
        except OSError:
            pass
    lines.append("```")
    lines.append("")

    body = "\n".join(lines) + "\n"
    with log_file.open("a", encoding="utf-8") as fh:
        fh.write(body)

    print(f"Session log written: {log_file}", file=sys.stderr)
    return log_file
