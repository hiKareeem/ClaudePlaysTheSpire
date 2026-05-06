"""Pytest configuration for the Python test suite.

Adds `tools/` to sys.path so tests can import `spire_bridge` directly,
and isolates each test by giving it a fresh tmp IPC dir + reloading
the module so module-level state (`_NEXT_ID`, `_CMD_COUNT`, `_FINDINGS`,
`_RUN_START`) is reset.
"""

from __future__ import annotations

import importlib
import os
import sys
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parent.parent
TOOLS_DIR = REPO_ROOT / "tools"

if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))


@pytest.fixture()
def ipc_env(tmp_path, monkeypatch):
    """Point the lib at a fresh temp IPC dir and reload the module.

    Yields the module and the IPC dir Path. The module's global state
    (_NEXT_ID, _CMD_COUNT, _FINDINGS) is reset by the reload.
    """
    ipc_dir = tmp_path / "ipc"
    ipc_dir.mkdir()
    monkeypatch.setenv("SPIREBRIDGE_IPC_DIR", str(ipc_dir))
    monkeypatch.delenv("HERMES_IPC_DIR", raising=False)

    # Reload to reset module-level state.
    if "spire_bridge" in sys.modules:
        sb = importlib.reload(sys.modules["spire_bridge"])
    else:
        sb = importlib.import_module("spire_bridge")
    yield sb, ipc_dir


def write_state(ipc_dir: Path, revision: int, **extra) -> None:
    import json

    payload = {"revision": revision, **extra}
    (ipc_dir / "state.json").write_text(json.dumps(payload), encoding="utf-8")
