"""Mock-bridge integration test for spire_bridge.

Spawns a tiny in-process bridge thread that watches commands.json and
writes result.json + bumps state.json revision. Exercises the full
send_bridge_command flow end-to-end (command write \u2192 result poll \u2192
revision wait) against a realistic file-IPC counterparty.

This is the "Layer 2" test from docs/port-autopilot-lib-python.md, scoped
down: we do not also spawn the PowerShell lib for parity comparison
(that's deferred per the spec).
"""

from __future__ import annotations

import json
import threading
import time
from pathlib import Path


class MockBridge:
    """Background thread that mimics the C# bridge's IPC loop.

    Watches `commands.json`; on each new command id, writes
    `result.json` with `status="ok"` and bumps `state.json` revision.
    Stops when `stop()` is called.
    """

    def __init__(self, ipc_dir: Path, *, poll_interval: float = 0.02):
        self.ipc_dir = ipc_dir
        self.poll_interval = poll_interval
        self._stop = threading.Event()
        self._thread: threading.Thread | None = None
        self._seen_ids: set[int] = set()
        self.commands_handled: list[dict] = []
        self._revision = 1

    def start(self) -> None:
        # Seed state.json with revision 1.
        (self.ipc_dir / "state.json").write_text(
            json.dumps({"revision": self._revision, "screen": {"name": "MainMenu"}}),
            encoding="utf-8",
        )
        self._thread = threading.Thread(target=self._loop, daemon=True)
        self._thread.start()

    def stop(self) -> None:
        self._stop.set()
        if self._thread:
            self._thread.join(timeout=2)

    def _loop(self) -> None:
        cmd_path = self.ipc_dir / "commands.json"
        res_path = self.ipc_dir / "result.json"
        state_path = self.ipc_dir / "state.json"
        while not self._stop.is_set():
            if cmd_path.is_file():
                try:
                    payload = json.loads(cmd_path.read_text(encoding="utf-8"))
                except (OSError, json.JSONDecodeError):
                    time.sleep(self.poll_interval)
                    continue
                cmd_id = int(payload.get("id", -1))
                if cmd_id >= 0 and cmd_id not in self._seen_ids:
                    self._seen_ids.add(cmd_id)
                    self.commands_handled.append(payload)
                    res_path.write_text(
                        json.dumps({"id": cmd_id, "status": "ok"}),
                        encoding="utf-8",
                    )
                    # Brief gap, then bump state revision.
                    time.sleep(0.03)
                    self._revision += 1
                    state_path.write_text(
                        json.dumps({
                            "revision": self._revision,
                            "screen": {"name": "MainMenu"},
                        }),
                        encoding="utf-8",
                    )
            time.sleep(self.poll_interval)


def test_mock_bridge_full_flow(ipc_env):
    """Full send_bridge_command flow against an in-process mock bridge.

    Sends three commands, asserts ok=True for each, and checks that the
    bridge saw them in order with monotonically increasing ids.
    """
    sb, ipc = ipc_env
    bridge = MockBridge(ipc)
    bridge.start()
    try:
        results = [
            sb.send_bridge_command({"type": "StartRun", "character": "IRONCLAD"}),
            sb.send_bridge_command({"type": "PlayCard", "handIndex": 0}),
            sb.send_bridge_command({"type": "EndTurn"}),
        ]
    finally:
        bridge.stop()

    for r in results:
        assert r.result is not None
        assert r.result["status"] == "ok"
        assert r.ok is True
        assert r.stalled is False
        assert r.state is not None

    # Ids are strictly increasing.
    ids = [r.id for r in results]
    assert ids == sorted(ids) and len(set(ids)) == len(ids)

    # Bridge saw all three in order.
    assert len(bridge.commands_handled) == 3
    assert [p["command"]["type"] for p in bridge.commands_handled] == [
        "StartRun", "PlayCard", "EndTurn",
    ]
    assert [p["id"] for p in bridge.commands_handled] == ids


def test_mock_bridge_stall_detection(ipc_env):
    """Bridge replies but does not bump revision \u2192 stalled=True."""
    sb, ipc = ipc_env
    # Seed initial state.
    (ipc / "state.json").write_text(
        json.dumps({"revision": 1, "screen": {"name": "MainMenu"}}),
        encoding="utf-8",
    )

    stop = threading.Event()

    def reply_only():
        cmd_path = ipc / "commands.json"
        while not stop.is_set():
            if cmd_path.is_file():
                payload = json.loads(cmd_path.read_text(encoding="utf-8"))
                (ipc / "result.json").write_text(
                    json.dumps({"id": payload["id"], "status": "ok"}),
                    encoding="utf-8",
                )
                return
            time.sleep(0.02)

    t = threading.Thread(target=reply_only, daemon=True)
    t.start()
    try:
        res = sb.send_bridge_command(
            {"type": "Noop"},
            result_timeout_sec=5,
            revision_timeout_sec=1,
        )
    finally:
        stop.set()
        t.join(timeout=2)

    assert res.result is not None
    assert res.result["status"] == "ok"
    assert res.stalled is True
    assert res.ok is False
    findings = sb.get_findings()
    assert any("STALL" in f for f in findings)


def test_mock_bridge_error_status(ipc_env):
    """Bridge replies with status=error \u2192 ok=False, finding logged."""
    sb, ipc = ipc_env
    (ipc / "state.json").write_text(
        json.dumps({"revision": 1, "screen": {"name": "MainMenu"}}),
        encoding="utf-8",
    )

    stop = threading.Event()

    def reply_error():
        cmd_path = ipc / "commands.json"
        while not stop.is_set():
            if cmd_path.is_file():
                payload = json.loads(cmd_path.read_text(encoding="utf-8"))
                (ipc / "result.json").write_text(
                    json.dumps({
                        "id": payload["id"],
                        "status": "error",
                        "message": "card not playable",
                    }),
                    encoding="utf-8",
                )
                # Bump revision so we exit the wait_revision branch quickly.
                time.sleep(0.03)
                (ipc / "state.json").write_text(
                    json.dumps({"revision": 2, "screen": {"name": "MainMenu"}}),
                    encoding="utf-8",
                )
                return
            time.sleep(0.02)

    t = threading.Thread(target=reply_error, daemon=True)
    t.start()
    try:
        res = sb.send_bridge_command(
            {"type": "PlayCard", "handIndex": 99},
            result_timeout_sec=5,
            revision_timeout_sec=5,
        )
    finally:
        stop.set()
        t.join(timeout=2)

    assert res.result is not None
    assert res.result["status"] == "error"
    assert res.ok is False
    assert res.stalled is False
    findings = sb.get_findings()
    assert any("CMD_ERROR" in f for f in findings)
