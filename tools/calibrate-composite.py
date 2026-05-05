#!/usr/bin/env python3
"""Score all v0 runs with the proposed composite formula and display ranking.

Formula (per protocol-v1 §Primary metric):
    composite = floor_reached + 10*act_reached + 50*victory - 0.1*ipc_error_count

Used to verify the formula discriminates v0 runs sensibly before freeze.
The intent is human eyeball calibration, not automatic tuning.
"""
import csv
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")

ROOT = Path(__file__).resolve().parents[1]
CSV = ROOT / "docs" / "benchmark" / "runs.csv"


def to_int(s, default=0):
    s = (s or "").strip()
    if not s:
        return default
    try:
        return int(s)
    except ValueError:
        try:
            return int(float(s))
        except ValueError:
            return default


def composite(row, w_act=10, w_win=50, w_err=0.1):
    """Compute composite_score using death_floor as floor_reached for non-wins."""
    floor = to_int(row.get("death_floor")) or to_int(row.get("victory_floor"))
    act = to_int(row.get("act_reached"))
    win = 1 if (row.get("halt_reason") == "victory" or row.get("victory_floor", "").strip()) else 0
    errs = to_int(row.get("ipc_error_count"))
    return floor + w_act * act + w_win * win - w_err * errs


def main():
    rows = list(csv.DictReader(CSV.open(encoding="utf-8")))
    scored = []
    for r in rows:
        s = composite(r)
        scored.append({
            "score": s,
            "run": r["run_id"].split("-run")[-1],
            "model": r["model"],
            "char": r["character"][:5],
            "halt": r["halt_reason"],
            "floor": to_int(r.get("death_floor")) or to_int(r.get("victory_floor")),
            "act": to_int(r.get("act_reached")),
            "errs": to_int(r.get("ipc_error_count")),
            "killed_by": r.get("killed_by", "").replace("ENCOUNTER.", ""),
        })

    scored.sort(key=lambda x: -x["score"])

    print(f"v0 run scoring (n={len(scored)}) — formula: floor + 10*act + 50*win - 0.1*errors")
    print()
    print(f"{'rank':>4} {'score':>7}  {'run':>3}  {'model':<24}  {'char':<6}  "
          f"{'halt':<8}  {'flr':>3}  {'act':>3}  {'err':>3}  killed_by")
    print("-" * 100)
    for i, s in enumerate(scored, 1):
        print(f"{i:>4} {s['score']:>7.1f}  {s['run']:>3}  {s['model']:<24}  {s['char']:<6}  "
              f"{s['halt']:<8}  {s['floor']:>3}  {s['act']:>3}  {s['errs']:>3}  {s['killed_by']}")

    print()
    print("=== Spread analysis ===")
    scores = [s["score"] for s in scored]
    print(f"min={min(scores):.1f}  max={max(scores):.1f}  range={max(scores)-min(scores):.1f}")
    print(f"unique scores: {len(set(scores))}/{len(scores)} (collisions = rank ties)")

    # Tie inspection
    from collections import Counter
    ties = [(score, count) for score, count in Counter(scores).items() if count > 1]
    if ties:
        print(f"\n=== Tied scores ({len(ties)} groups) ===")
        for score, count in sorted(ties, key=lambda x: -x[0]):
            tied = [s for s in scored if s["score"] == score]
            print(f"  score={score:.1f} ({count} runs):")
            for t in tied:
                print(f"    run{t['run']:>3}  {t['model']:<22}  {t['char']:<6}  flr={t['floor']:>2}  act={t['act']}  errs={t['errs']}")

    # Per-character breakdown
    print("\n=== Per-character ranges ===")
    chars = {}
    for s in scored:
        chars.setdefault(s["char"], []).append(s["score"])
    for c, ss in sorted(chars.items()):
        print(f"  {c:<6}  n={len(ss)}  min={min(ss):.1f}  max={max(ss):.1f}  median={sorted(ss)[len(ss)//2]:.1f}")

    # Per-model breakdown
    print("\n=== Per-model ranges ===")
    models = {}
    for s in scored:
        models.setdefault(s["model"], []).append(s["score"])
    for m, ss in sorted(models.items(), key=lambda x: -sum(x[1])/len(x[1])):
        print(f"  {m:<26}  n={len(ss)}  min={min(ss):.1f}  max={max(ss):.1f}  mean={sum(ss)/len(ss):.1f}")


if __name__ == "__main__":
    main()
