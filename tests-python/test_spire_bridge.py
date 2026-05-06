"""Unit tests for spire_bridge.

Layer 1 of the test plan in docs/port-autopilot-lib-python.md:
read_state retry, wait_revision, send_bridge_command shapes, findings,
session round-trip, env-var resolution, multi-instance isolation.
"""

from __future__ import annotations

import importlib
import json
import sys
import threading
import time
from pathlib import Path

import pytest

from conftest import write_state


# ---------------- Env-var resolution ----------------


def _reload(monkeypatch, **env):
    """Reload spire_bridge with a fresh environment.

    `env` values that are None are unset; others are set on the env.
    """
    for key in ("SPIREBRIDGE_IPC_DIR", "HERMES_IPC_DIR", "APPDATA"):
        if key in env:
            value = env[key]
            if value is None:
                monkeypatch.delenv(key, raising=False)
            else:
                monkeypatch.setenv(key, value)
    if "spire_bridge" in sys.modules:
        return importlib.reload(sys.modules["spire_bridge"])
    return importlib.import_module("spire_bridge")


def test_env_resolution_spirebridge_wins(tmp_path, monkeypatch):
    spire = tmp_path / "spire"
    hermes = tmp_path / "hermes"
    spire.mkdir(); hermes.mkdir()
    sb = _reload(
        monkeypatch,
        SPIREBRIDGE_IPC_DIR=str(spire),
        HERMES_IPC_DIR=str(hermes),
    )
    assert sb.get_ipc_paths().ipc_dir == spire


def test_env_resolution_hermes_fallback_warns(tmp_path, monkeypatch, capsys):
    hermes = tmp_path / "hermes"
    hermes.mkdir()
    sb = _reload(
        monkeypatch,
        SPIREBRIDGE_IPC_DIR=None,
        HERMES_IPC_DIR=str(hermes),
    )
    # First resolve emits the deprecation warning.
    paths = sb.get_ipc_paths()
    assert paths.ipc_dir == hermes
    err = capsys.readouterr().err
    assert "HERMES_IPC_DIR is deprecated" in err

    # Second resolve does not duplicate the warning.
    sb.get_ipc_paths()
    err2 = capsys.readouterr().err
    assert "HERMES_IPC_DIR is deprecated" not in err2


def test_env_resolution_appdata_default(tmp_path, monkeypatch):
    appdata = tmp_path / "appdata"
    appdata.mkdir()
    sb = _reload(
        monkeypatch,
        SPIREBRIDGE_IPC_DIR=None,
        HERMES_IPC_DIR=None,
        APPDATA=str(appdata),
    )
    expected = appdata / "SlayTheSpire2" / "hermesbridge"
    assert sb.get_ipc_paths().ipc_dir == expected


def test_env_resolution_no_appdata_raises(monkeypatch):
    sb = _reload(
        monkeypatch,
        SPIREBRIDGE_IPC_DIR=None,
        HERMES_IPC_DIR=None,
        APPDATA=None,
    )
    with pytest.raises(FileNotFoundError):
        sb.get_ipc_paths()


# ---------------- read_state retry ----------------


def test_read_state_retries_until_present(ipc_env):
    sb, ipc = ipc_env
    state_path = ipc / "state.json"

    def write_after_delay():
        time.sleep(0.2)
        state_path.write_text(json.dumps({"revision": 7}), encoding="utf-8")

    t = threading.Thread(target=write_after_delay)
    t.start()
    state = sb.read_state()
    t.join()
    assert state["revision"] == 7


def test_read_state_persistent_failure_raises(ipc_env):
    sb, _ipc = ipc_env
    with pytest.raises(RuntimeError):
        sb.read_state()


# ---------------- wait_revision ----------------


def test_wait_revision_returns_none_on_timeout(ipc_env):
    sb, ipc = ipc_env
    write_state(ipc, revision=5)
    start = time.monotonic()
    state = sb.wait_revision(after_revision=5, timeout_sec=1)
    elapsed = time.monotonic() - start
    assert state is None
    assert elapsed >= 1.0


def test_wait_revision_returns_state_on_bump(ipc_env):
    sb, ipc = ipc_env
    write_state(ipc, revision=5)

    def bump():
        time.sleep(0.3)
        write_state(ipc, revision=6, marker="bumped")

    t = threading.Thread(target=bump)
    t.start()
    state = sb.wait_revision(after_revision=5, timeout_sec=5)
    t.join()
    assert state is not None
    assert state["revision"] == 6
    assert state["marker"] == "bumped"


# ---------------- clear_ipc ----------------


def test_clear_ipc_removes_stale_files(ipc_env):
    sb, ipc = ipc_env
    (ipc / "commands.json").write_text("stale", encoding="utf-8")
    (ipc / "result.json").write_text("stale", encoding="utf-8")
    (ipc / "state.json").write_text("{}", encoding="utf-8")

    sb.clear_ipc()

    assert not (ipc / "commands.json").exists()
    assert not (ipc / "result.json").exists()
    # state.json must NOT be cleared.
    assert (ipc / "state.json").exists()


# ---------------- findings ----------------


def test_log_finding_accumulates(ipc_env):
    sb, _ipc = ipc_env
    sb.log_finding("first")
    sb.log_finding("second")
    findings = sb.get_findings()
    assert len(findings) == 2
    assert "first" in findings[0]
    assert "second" in findings[1]


def test_get_findings_returns_copy(ipc_env):
    sb, _ipc = ipc_env
    sb.log_finding("only")
    findings = sb.get_findings()
    findings.clear()
    assert len(sb.get_findings()) == 1


# ---------------- session state ----------------


def test_session_state_round_trip(ipc_env, monkeypatch):
    sb, ipc = ipc_env
    # Trigger a save with non-default values.
    sb.reset_session(starting_id=42)
    # Manually bump _CMD_COUNT and persist (simulating a sent command).
    sb_module = sys.modules["spire_bridge"]
    sb_module._NEXT_ID = 43
    sb_module._CMD_COUNT = 1
    sb_module._save_session_state()

    sess_path = ipc / "autopilot-session.json"
    assert sess_path.is_file()
    payload = json.loads(sess_path.read_text(encoding="utf-8"))
    assert payload["nextId"] == 43
    assert payload["cmdCount"] == 1

    # Reload and confirm the new module reads it back.
    monkeypatch.setenv("SPIREBRIDGE_IPC_DIR", str(ipc))
    sb2 = importlib.reload(sys.modules["spire_bridge"])
    assert sb2._NEXT_ID == 43
    assert sb2._CMD_COUNT == 1


def test_reset_session_keeps_max_id(ipc_env):
    sb, _ipc = ipc_env
    sb.reset_session(starting_id=10)
    sb_module = sys.modules["spire_bridge"]
    sb_module._NEXT_ID = 50
    sb.reset_session(starting_id=5)
    # max(5, 50) = 50: do not regress the id counter.
    assert sb_module._NEXT_ID == 50


# ---------------- multi-instance isolation ----------------


def test_multi_instance_isolation(tmp_path, monkeypatch):
    """Two different IPC dirs must not see each other's state."""
    a = tmp_path / "a"
    b = tmp_path / "b"
    a.mkdir(); b.mkdir()

    monkeypatch.setenv("SPIREBRIDGE_IPC_DIR", str(a))
    sb_a = importlib.reload(sys.modules["spire_bridge"]) \
        if "spire_bridge" in sys.modules \
        else importlib.import_module("spire_bridge")
    write_state(a, revision=1, slot="A")
    state_a = sb_a.read_state()
    assert state_a["slot"] == "A"

    monkeypatch.setenv("SPIREBRIDGE_IPC_DIR", str(b))
    sb_b = importlib.reload(sys.modules["spire_bridge"])
    # No state.json in b yet — read should fail.
    with pytest.raises(RuntimeError):
        sb_b.read_state()
    write_state(b, revision=99, slot="B")
    state_b = sb_b.read_state()
    assert state_b["slot"] == "B"

    # And A's state file is untouched on disk.
    state_a_again = json.loads((a / "state.json").read_text(encoding="utf-8"))
    assert state_a_again["slot"] == "A"


# ---------------- send_bridge_command shapes ----------------


def test_send_bridge_command_ipc_timeout(ipc_env):
    sb, ipc = ipc_env
    write_state(ipc, revision=1)
    res = sb.send_bridge_command(
        {"type": "Noop"},
        result_timeout_sec=1,
        revision_timeout_sec=1,
    )
    assert res.result is None
    assert res.ok is False
    assert res.stalled is False
    assert isinstance(res.id, int)
    findings = sb.get_findings()
    assert any("IPC_TIMEOUT" in f for f in findings)


def test_send_bridge_command_writes_correct_payload(ipc_env):
    sb, ipc = ipc_env
    write_state(ipc, revision=1)

    # Don't wait for a result — we just want to inspect the cmd file.
    captured: list[dict] = []

    def watch_and_reply():
        cmd_path = ipc / "commands.json"
        for _ in range(50):
            if cmd_path.is_file():
                payload = json.loads(cmd_path.read_text(encoding="utf-8"))
                captured.append(payload)
                # Echo a result with matching id, then bump revision.
                (ipc / "result.json").write_text(
                    json.dumps({"id": payload["id"], "status": "ok"}),
                    encoding="utf-8",
                )
                time.sleep(0.05)
                write_state(ipc, revision=2)
                return
            time.sleep(0.02)

    t = threading.Thread(target=watch_and_reply)
    t.start()
    res = sb.send_bridge_command(
        {"type": "PlayCard", "handIndex": 0},
        result_timeout_sec=5,
        revision_timeout_sec=5,
    )
    t.join()

    assert len(captured) == 1
    payload = captured[0]
    assert payload["command"]["type"] == "PlayCard"
    assert payload["command"]["handIndex"] == 0
    assert isinstance(payload["id"], int)
    assert res.ok is True
    assert res.stalled is False
    assert res.state["revision"] == 2
    assert res.id == payload["id"]
