#!/usr/bin/env python3
"""
trial-schedule.py - generate the deterministic run schedule for a SpireBench trial.

USAGE:
  python tools/operator/trial-schedule.py --trial trial-v1
  python tools/operator/trial-schedule.py --trial trial-v1 --seed-source-seed 20260506 --k 3 --priors A0,B0
  python tools/operator/trial-schedule.py --trial trial-v1 --dry-run

OUTPUT:
  docs/benchmark/<trial>-schedule.csv

The schedule is the single source of truth for "what is run #N?".
Re-running with identical args produces identical CSV (deterministic).

CSV columns:
  run_id          1-indexed sequential run number for the trial
  trial           trial name (trial-v1)
  model           model id (gpt-5.5, claude-opus-4.7, ...)
  character       IRONCLAD | SILENT | DEFECT | REGENT | NECROBINDER
  prior           A0 (zero-shot) | B0 (with priors)
  k_index         1..k (which repeat within the cell)
  seed            64-bit unsigned game seed -- PAIRED across the cohort:
                  rows that share (character, k_index) share the same seed,
                  so every (model x prior) combo in a cell plays the same
                  three maps. This is the core of the paired-3-seeds design.
  seed_label      alpha (k=1) | beta (k=2) | gamma (k=3). Same label inside
                  a (character, seed) row group; required by the run-record
                  schema (protocol-v1 sec.149).
  slot_assigned   slot label set when start-run claims it ("" until claimed)
  status          pending | in_progress | complete | failed
  started_utc     ISO timestamp when start-run claimed it
  completed_utc   ISO timestamp when finalize wrote the record

Slot column lets multi-instance operation route runs without conflict.
Status transitions: pending -> in_progress -> complete (or failed).

For trial-v1 defaults:
  models    = gpt-5.5, claude-opus-4.7, deepseek-v4-pro, gemini-3.1-pro-preview, glm-5.1
  chars     = IRONCLAD, SILENT, DEFECT, REGENT, NECROBINDER
  priors    = A0, B0
  k         = 3
  total     = 5 * 5 * 2 * 3 = 150 runs

Run ordering: stable interleave so models/characters distribute evenly across
the schedule. Avoids long blocks of one model (which would stall progress
visibility) and avoids back-to-back same-character runs on a single slot.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import os
import random
import sys
from pathlib import Path

# Defaults locked at trial-v1 spec time. CHANGING THESE BREAKS DETERMINISM
# for any seed-source-seed previously used. To re-roll, use a new seed.
DEFAULT_MODELS = [
    "gpt-5.5",
    "claude-opus-4.7",
    "deepseek-v4-pro",
    "gemini-3.1-pro-preview",
    "glm-5.1",
]
DEFAULT_CHARACTERS = [
    "IRONCLAD",
    "SILENT",
    "DEFECT",
    "REGENT",
    "NECROBINDER",
]
DEFAULT_PRIORS = ["A0", "B0"]
DEFAULT_K = 3
DEFAULT_SEED_SOURCE = 20260506  # date trial-v1 was scheduled


# k_index -> seed_label mapping per protocol-v1 sec.149.
# Trial-v1 uses k=3 -> alpha/beta/gamma. If k>3 the labels run out and we
# fall back to "k<n>" for the extras; that's intentional, the operator
# should notice and either bump the spec or stop overshooting k.
SEED_LABELS = ["alpha", "beta", "gamma"]


def seed_label_for_k(k_index: int) -> str:
    if 1 <= k_index <= len(SEED_LABELS):
        return SEED_LABELS[k_index - 1]
    return f"k{k_index}"


def stable_seed_for_cell(seed_source: int, character: str, k_index: int) -> int:
    """Derive a deterministic 64-bit unsigned seed from (character, k_index).

    Critically, the seed depends on character + k_index ONLY -- not on
    model or prior. This is what makes the trial paired: every
    (model, prior) combo in the (character, k_index) cohort plays the
    same map.

    SHA-256 is overkill but free, eliminates any concern about RNG-state
    contamination across cells, and lets us regenerate any single cell
    without re-rolling the whole schedule.
    """
    key = f"{seed_source}|{character}|{k_index}".encode("utf-8")
    digest = hashlib.sha256(key).digest()
    # Take first 8 bytes as unsigned 64-bit. Game accepts up to 2^63 in some
    # paths so we mask to 63 bits to stay safe across all StS2 seed inputs.
    val = int.from_bytes(digest[:8], "big") & ((1 << 63) - 1)
    return val


def interleave_cells(cells: list[dict], rng: random.Random) -> list[dict]:
    """Order cells so models AND characters distribute evenly through the trial.

    Two-pass strategy:
      Pass 1: bucket by model, shuffle within buckets, round-robin across
              models so each row rotates the model.
      Pass 2: local repair. For any (i, i+2) pair where model and character
              both repeat (which would put two same-(model,char) runs on the
              same slot under naive parity assignment), look ahead for a
              swap candidate that breaks the collision without creating a
              worse one. Repeat until clean or exhaustion.

    Result: no slot will play the same character twice in a row when slots
    take strictly alternating rows. Real-world dynamic slot assignment
    (drift between slots) makes this even safer.
    """
    by_model: dict[str, list[dict]] = {}
    for c in cells:
        by_model.setdefault(c["model"], []).append(c)

    for model in by_model:
        rng.shuffle(by_model[model])

    ordered: list[dict] = []
    model_keys = list(by_model.keys())
    rng.shuffle(model_keys)

    while any(by_model[m] for m in model_keys):
        for m in model_keys:
            if by_model[m]:
                ordered.append(by_model[m].pop(0))

    # Pass 2: minimize same-character runs at distance 2 (slot collision).
    # Bounded passes; this is heuristic, not exact.
    for _ in range(8):
        swaps = 0
        for i in range(len(ordered) - 2):
            if ordered[i]["character"] == ordered[i + 2]["character"]:
                # Find a swap target j > i+2 that:
                #   - has a different character from ordered[i] AND ordered[i+4 if exists]
                #   - and the swap doesn't create a worse same-char-distance-2 collision
                for j in range(i + 3, len(ordered)):
                    cand = ordered[j]
                    # Don't break model parity: only swap with same-position-modulo
                    # row to keep the model rotation. (Both at odd, or both at even.)
                    if (j - (i + 2)) % len(by_model) != 0:
                        continue
                    if cand["model"] != ordered[i + 2]["model"]:
                        continue
                    if cand["character"] == ordered[i]["character"]:
                        continue
                    # Check the swap doesn't make the j-position worse
                    j_prev = ordered[j - 2]["character"] if j >= 2 else None
                    j_next = ordered[j + 2]["character"] if j + 2 < len(ordered) else None
                    if ordered[i + 2]["character"] in (j_prev, j_next):
                        continue
                    ordered[i + 2], ordered[j] = ordered[j], ordered[i + 2]
                    swaps += 1
                    break
        if swaps == 0:
            break
    return ordered


def build_schedule(
    trial: str,
    models: list[str],
    characters: list[str],
    priors: list[str],
    k: int,
    seed_source: int,
) -> list[dict]:
    """Build the full schedule as a list of row dicts."""
    cells: list[dict] = []
    for model in models:
        for character in characters:
            for prior in priors:
                for k_index in range(1, k + 1):
                    seed = stable_seed_for_cell(seed_source, character, k_index)
                    cells.append(
                        {
                            "trial": trial,
                            "model": model,
                            "character": character,
                            "prior": prior,
                            "k_index": k_index,
                            "seed": seed,
                            "seed_label": seed_label_for_k(k_index),
                            "slot_assigned": "",
                            "status": "pending",
                            "started_utc": "",
                            "completed_utc": "",
                        }
                    )

    # Deterministic interleave: seed PRNG from seed_source so re-runs match.
    rng = random.Random(seed_source ^ 0x5C5C_5C5C)
    ordered = interleave_cells(cells, rng)

    # Assign 1-indexed run_id in final order
    for i, row in enumerate(ordered, start=1):
        row["run_id"] = i
    return ordered


def write_csv(path: Path, rows: list[dict]) -> None:
    fieldnames = [
        "run_id",
        "trial",
        "model",
        "character",
        "prior",
        "k_index",
        "seed",
        "seed_label",
        "slot_assigned",
        "status",
        "started_utc",
        "completed_utc",
    ]
    # Use \n line terminator + utf-8 to keep diffs clean across OSes.
    with path.open("w", newline="\n", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        for r in rows:
            writer.writerow({k: r[k] for k in fieldnames})


def parse_args(argv: list[str]) -> argparse.Namespace:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--trial", default="trial-v1", help="trial name (default trial-v1)")
    p.add_argument(
        "--seed-source-seed",
        type=int,
        default=DEFAULT_SEED_SOURCE,
        help=f"PRNG seed for deterministic schedule generation (default {DEFAULT_SEED_SOURCE})",
    )
    p.add_argument("--k", type=int, default=DEFAULT_K, help=f"repeats per cell (default {DEFAULT_K})")
    p.add_argument(
        "--models",
        default=",".join(DEFAULT_MODELS),
        help="comma-separated model ids",
    )
    p.add_argument(
        "--characters",
        default=",".join(DEFAULT_CHARACTERS),
        help="comma-separated character ids",
    )
    p.add_argument(
        "--priors",
        default=",".join(DEFAULT_PRIORS),
        help="comma-separated knowledge conditions",
    )
    p.add_argument(
        "--output",
        default=None,
        help="output path (default docs/benchmark/<trial>-schedule.csv)",
    )
    p.add_argument("--dry-run", action="store_true", help="print summary; do not write file")
    p.add_argument("--force", action="store_true", help="overwrite existing schedule (DESTRUCTIVE)")
    return p.parse_args(argv)


def main(argv: list[str]) -> int:
    args = parse_args(argv)

    repo_root = Path(__file__).resolve().parents[2]
    out_path = (
        Path(args.output)
        if args.output
        else repo_root / "docs" / "benchmark" / f"{args.trial}-schedule.csv"
    )

    models = [m.strip() for m in args.models.split(",") if m.strip()]
    characters = [c.strip() for c in args.characters.split(",") if c.strip()]
    priors = [p.strip() for p in args.priors.split(",") if p.strip()]

    rows = build_schedule(
        trial=args.trial,
        models=models,
        characters=characters,
        priors=priors,
        k=args.k,
        seed_source=args.seed_source_seed,
    )

    print(f"trial:          {args.trial}")
    print(f"seed-source:    {args.seed_source_seed}")
    print(f"models ({len(models)}):    {', '.join(models)}")
    print(f"chars  ({len(characters)}):    {', '.join(characters)}")
    print(f"priors ({len(priors)}):    {', '.join(priors)}")
    print(f"k:              {args.k}")
    print(f"total runs:     {len(rows)}")
    print(f"output:         {out_path}")

    if args.dry_run:
        print()
        print("DRY RUN. First 5 rows:")
        for r in rows[:5]:
            print(f"  run_id={r['run_id']:3d}  {r['model']:24s}  {r['character']:11s}  {r['prior']}  k={r['k_index']} ({r['seed_label']:5s})  seed={r['seed']}")
        return 0

    if out_path.exists() and not args.force:
        print()
        print(f"ERROR: {out_path} already exists. Use --force to overwrite (destroys progress).")
        return 2

    out_path.parent.mkdir(parents=True, exist_ok=True)
    write_csv(out_path, rows)
    print()
    print(f"Wrote {len(rows)} rows.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
