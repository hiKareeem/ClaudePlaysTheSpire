#!/usr/bin/env python3
"""
regenerate-runs-csv.py - Rebuild docs/benchmark/runs.csv from scratch by
reading the YAML frontmatter of every run record in docs/benchmark/runs/*.md.

Maintainer-only. Use after parse-run-history.py adds .run-derived stats
to existing run records, or when the schema needs extension.

Usage:
  python tools/maintainer/regenerate-runs-csv.py [--dry-run]

The output column order is the union of:
  1. existing runs.csv header (preserving order)
  2. STATS_KEYS from parse-run-history.py (appended after, in order)

Seed is force-quoted to dodge Excel scientific-notation; null/missing
fields are written as the empty string. Idempotent.
"""

from __future__ import annotations

import argparse
import csv
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
RUNS_DIR = REPO_ROOT / "docs" / "benchmark" / "runs"
CSV_PATH = REPO_ROOT / "docs" / "benchmark" / "runs.csv"

STATS_KEYS = [
    "act_reached",
    "total_floors",
    "total_card_picks",
    "total_card_skips",
    "total_relics_picked",
    "total_potions_used",
    "total_potions_bought",
    "total_damage_taken",
    "total_gold_gained",
    "total_gold_spent",
    "total_gold_lost",
    "total_hp_healed",
    "elites_fought",
    "rests_taken",
    "shops_visited",
    "events_visited",
    "rest_choice_heal",
    "rest_choice_smith",
    "killed_by",
    "was_abandoned",
    "run_time_seconds",
]

FM_RE = re.compile(r"\A---\s*\r?\n(.*?)\r?\n---", re.DOTALL)
KV_RE = re.compile(r"^([a-z_][a-z0-9_]*):\s*(.*?)\s*$")


def parse_frontmatter(text: str) -> dict[str, str] | None:
    m = FM_RE.match(text)
    if not m:
        return None
    out: dict[str, str] = {}
    for line in m.group(1).splitlines():
        if line.startswith("#"):
            continue
        km = KV_RE.match(line)
        if km:
            out[km.group(1)] = km.group(2)
    return out


def normalize(key: str, val: str) -> str:
    val = val.strip()
    if (len(val) >= 2) and val[0] == val[-1] and val[0] in ('"', "'"):
        val = val[1:-1]
    if val.lower() == "null":
        return ""
    return val


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    if not CSV_PATH.exists():
        print(f"ERROR: {CSV_PATH} missing", file=sys.stderr)
        return 2

    existing_header = CSV_PATH.read_text(encoding="utf-8").splitlines()[0].split(",")

    # build column order: existing header first, then any STATS_KEYS not yet in it
    cols: list[str] = list(existing_header)
    for k in STATS_KEYS:
        if k not in cols:
            cols.append(k)

    rows = []
    md_files = sorted(RUNS_DIR.glob("*.md"))
    for p in md_files:
        fm = parse_frontmatter(p.read_text(encoding="utf-8"))
        if not fm or "run_id" not in fm:
            continue
        row = {}
        for c in cols:
            row[c] = normalize(c, fm.get(c, ""))
        rows.append(row)

    rows.sort(key=lambda r: r["run_id"])

    print(f"columns ({len(cols)}): {','.join(cols)}")
    print(f"rows: {len(rows)}")

    if args.dry_run:
        for r in rows:
            print(f"  {r['run_id']}  act={r.get('act_reached','')}  killed_by={r.get('killed_by','')}")
        return 0

    # write atomically
    tmp = CSV_PATH.with_suffix(".csv.tmp")
    with tmp.open("w", encoding="utf-8", newline="") as f:
        # QUOTE_MINIMAL handles embedded commas/quotes; for seed we want
        # explicit quoting to dodge spreadsheet scientific-notation, so
        # write the seed column ourselves.
        f.write(",".join(cols) + "\n")
        for r in rows:
            out_row = []
            for c in cols:
                v = r[c]
                if v == "":
                    out_row.append("")
                elif c == "seed":
                    # always quote seed; never contains commas/quotes itself
                    out_row.append(f'"{v}"')
                elif "," in v or '"' in v or "\n" in v:
                    esc = v.replace('"', '""')
                    out_row.append(f'"{esc}"')
                else:
                    out_row.append(v)
            f.write(",".join(out_row) + "\n")
    tmp.replace(CSV_PATH)
    print(f"WROTE {CSV_PATH}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
