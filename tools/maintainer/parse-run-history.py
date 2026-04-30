#!/usr/bin/env python3
"""
parse-run-history.py - Match StS2 .run files to SpireBench run records by seed,
archive the raw .run alongside the run record, and emit a YAML fragment of
extracted statistics ready to splice into the run record's frontmatter (and a
matching CSV row tail for runs.csv).

Maintainer-only tool. Not shipped in the public bundle.

Usage:
  python tools/maintainer/parse-run-history.py [--run-id <id>] [--all] [--dry-run] [--write]

Modes:
  --run-id <id>     Process exactly one run (by run_id from frontmatter).
  --all             Process every run record in docs/benchmark/runs/*.md.
  --dry-run         (default) Print what would be done; don't modify anything.
  --write           Archive the .run file as runs/<run_id>.run AND patch the
                    run record's frontmatter with the extracted stats fields.
                    runs.csv is NOT touched here -- emit the fragment to stdout
                    and you decide whether to extend the schema.

Matching strategy:
  1. Read seed + start_time_utc from each run record's YAML frontmatter.
  2. If seed is present (not null/empty): scan history dir for any .run
     whose top-level seed equals the record's seed. Unique match wins.
  3. If seed is missing: convert start_time_utc -> unix epoch and scan for
     .run files whose filename (= unix epoch of run start) is within
     +/- 600s of the record's start. Filter by character match. Pick the
     closest in time.
  4. Report ambiguous / zero matches without modifying anything.

Stats extracted (per matched .run):
  act_reached            int -- 1..4
  total_floors           int -- sum across all acts in map_point_history
  total_card_picks       int -- card_choices entries with was_picked=True
  total_card_skips       int -- entries with was_picked=False where the
                                 floor still produced no cards_gained
  total_relics_picked    int -- relic_choices was_picked=True
  total_potions_used     int -- sum of len(potion_used) per floor
  total_potions_bought   int -- sum of len(bought_potions) per floor
  total_damage_taken     int -- sum of player_stats[0].damage_taken
  total_gold_gained      int -- sum of gold_gained
  total_gold_spent       int -- sum of gold_spent
  total_gold_lost        int -- sum of gold_lost (events, theft)
  total_hp_healed        int -- sum of hp_healed
  elites_fought          int -- floors with map_point_type=='elite'
  rests_taken            int -- map_point_type=='rest_site' floors
  shops_visited          int -- map_point_type=='shop' floors
  events_visited         int -- map_point_type in ('unknown','ancient') &
                                 rooms[0].room_type=='event' (excluding Neow)
  rest_choice_heal       int -- count of REST/HEAL choices
  rest_choice_smith      int -- count of SMITH/UPGRADE choices
  killed_by              str -- killed_by_encounter (or 'none' / 'abandoned')
  was_abandoned          bool -- top-level was_abandoned
  run_time_seconds       int -- top-level run_time (game-clock, not wall)

Side-effects when --write:
  - copies <history_dir>/<unix>.run -> docs/benchmark/runs/<run_id>.run
  - rewrites the .md frontmatter, replacing stats keys if present, appending
    them just before the closing '---' if absent.
  - leaves the rest of the run record untouched.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import sys
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path

# ---- paths ------------------------------------------------------------------

REPO_ROOT = Path(__file__).resolve().parents[2]  # tools/maintainer/.. -> tools/.. -> repo root
RUNS_DIR = REPO_ROOT / "docs" / "benchmark" / "runs"
HISTORY_DIR = Path(os.environ["APPDATA"]) / "SlayTheSpire2" / "steam" / "76561198920102273" / "profile1" / "saves" / "history"

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

# ---- frontmatter parsing ----------------------------------------------------

FM_RE = re.compile(r"\A---\s*\r?\n(.*?)\r?\n---", re.DOTALL)
KV_RE = re.compile(r"^([a-z_][a-z0-9_]*):\s*(.*?)\s*$")


def parse_frontmatter(text: str) -> dict[str, str] | None:
    m = FM_RE.match(text)
    if not m:
        return None
    out: dict[str, str] = {}
    for line in m.group(1).splitlines():
        km = KV_RE.match(line)
        if km:
            out[km.group(1)] = km.group(2)
    return out


def strip_quotes(s: str) -> str:
    s = s.strip()
    if (len(s) >= 2) and s[0] == s[-1] and s[0] in ('"', "'"):
        return s[1:-1]
    return s


# ---- .run inspection --------------------------------------------------------

@dataclass
class RunFile:
    path: Path
    unix: int
    seed: str
    character: str
    start_time: int

    @classmethod
    def load(cls, p: Path) -> "RunFile | None":
        try:
            j = json.loads(p.read_text(encoding="utf-8"))
        except Exception:
            return None
        try:
            return cls(
                path=p,
                unix=int(p.stem),
                seed=str(j.get("seed", "")),
                character=str(j["players"][0].get("character", "")).replace("CHARACTER.", ""),
                start_time=int(j.get("start_time", 0)),
            )
        except Exception:
            return None


def scan_history() -> list[RunFile]:
    out: list[RunFile] = []
    for p in sorted(HISTORY_DIR.glob("*.run")):
        rf = RunFile.load(p)
        if rf:
            out.append(rf)
    return out


# ---- stats extraction -------------------------------------------------------

def extract_stats(run_path: Path) -> dict:
    j = json.loads(run_path.read_text(encoding="utf-8"))
    mph = j.get("map_point_history", [])
    acts = j.get("acts", [])

    total_floors = 0
    card_picks = 0
    card_skips = 0
    relic_picks = 0
    potions_used = 0
    potions_bought = 0
    dmg = 0
    gold_gained = 0
    gold_spent = 0
    gold_lost = 0
    hp_healed = 0
    elites = 0
    rests = 0
    shops = 0
    events = 0
    rest_heal = 0
    rest_smith = 0

    last_act_with_floors = 0

    for act_idx, act in enumerate(mph):
        if not isinstance(act, list):
            continue
        if act:
            last_act_with_floors = act_idx + 1  # 1-indexed
        for floor in act:
            total_floors += 1
            mpt = floor.get("map_point_type", "")
            ps_arr = floor.get("player_stats", [])
            ps = ps_arr[0] if ps_arr else {}

            dmg += int(ps.get("damage_taken", 0) or 0)
            gold_gained += int(ps.get("gold_gained", 0) or 0)
            gold_spent += int(ps.get("gold_spent", 0) or 0)
            gold_lost += int(ps.get("gold_lost", 0) or 0)
            hp_healed += int(ps.get("hp_healed", 0) or 0)

            for cc in ps.get("card_choices", []) or []:
                if isinstance(cc, dict):
                    if cc.get("was_picked"):
                        card_picks += 1
                    else:
                        card_skips += 1

            for rc in ps.get("relic_choices", []) or []:
                if isinstance(rc, dict) and rc.get("was_picked"):
                    relic_picks += 1

            potions_used += len(ps.get("potion_used", []) or [])
            potions_bought += len(ps.get("bought_potions", []) or [])

            for rsc in ps.get("rest_site_choices", []) or []:
                s = str(rsc).upper()
                if s in ("HEAL", "REST"):
                    rest_heal += 1
                elif s in ("SMITH", "UPGRADE", "FORGE"):
                    rest_smith += 1

            if mpt == "elite":
                elites += 1
            elif mpt == "rest_site":
                rests += 1
            elif mpt == "shop":
                shops += 1
            elif mpt in ("unknown", "ancient"):
                rooms = floor.get("rooms", []) or []
                if rooms and rooms[0].get("room_type") == "event":
                    model_id = str(rooms[0].get("model_id", ""))
                    if model_id != "EVENT.NEOW":
                        events += 1

    killed_by_enc = str(j.get("killed_by_encounter", "NONE.NONE"))
    killed_by_evt = str(j.get("killed_by_event", "NONE.NONE"))
    if j.get("was_abandoned"):
        killed_by_field = "abandoned"
    elif killed_by_enc not in ("NONE.NONE", ""):
        killed_by_field = killed_by_enc
    elif killed_by_evt not in ("NONE.NONE", ""):
        killed_by_field = killed_by_evt
    else:
        killed_by_field = "none"

    return {
        "act_reached": last_act_with_floors,
        "total_floors": total_floors,
        "total_card_picks": card_picks,
        "total_card_skips": card_skips,
        "total_relics_picked": relic_picks,
        "total_potions_used": potions_used,
        "total_potions_bought": potions_bought,
        "total_damage_taken": dmg,
        "total_gold_gained": gold_gained,
        "total_gold_spent": gold_spent,
        "total_gold_lost": gold_lost,
        "total_hp_healed": hp_healed,
        "elites_fought": elites,
        "rests_taken": rests,
        "shops_visited": shops,
        "events_visited": events,
        "rest_choice_heal": rest_heal,
        "rest_choice_smith": rest_smith,
        "killed_by": killed_by_field,
        "was_abandoned": bool(j.get("was_abandoned")),
        "run_time_seconds": int(j.get("run_time", 0) or 0),
    }


# ---- matching ---------------------------------------------------------------

def match_run(record: dict, history: list[RunFile]) -> tuple[RunFile | None, str]:
    """Return (matched RunFile or None, reason)."""
    seed_raw = strip_quotes(record.get("seed", ""))
    if seed_raw and seed_raw.lower() != "null":
        hits = [rf for rf in history if rf.seed == seed_raw]
        if len(hits) == 1:
            return hits[0], f"seed match ({seed_raw})"
        if len(hits) > 1:
            return None, f"AMBIGUOUS seed match: {[rf.path.name for rf in hits]}"

    # fallback: start_time_utc + character
    st_raw = strip_quotes(record.get("start_time_utc", ""))
    char = strip_quotes(record.get("character", "")).upper()
    if not st_raw or st_raw.lower() == "null":
        return None, "no seed and no start_time_utc"
    try:
        dt = datetime.fromisoformat(st_raw.replace("Z", "+00:00")).astimezone(timezone.utc)
    except Exception as e:
        return None, f"unparseable start_time_utc: {st_raw} ({e})"
    target = int(dt.timestamp())
    candidates = [
        rf for rf in history
        if rf.character == char and abs(rf.start_time - target) <= 600
    ]
    if not candidates:
        return None, f"no .run within +/-600s of {st_raw} for {char}"
    candidates.sort(key=lambda rf: abs(rf.start_time - target))
    return candidates[0], f"start_time/character fallback (delta={candidates[0].start_time - target}s)"


# ---- frontmatter patching ---------------------------------------------------

def patch_frontmatter(md_path: Path, stats: dict, seed: str) -> None:
    text = md_path.read_text(encoding="utf-8")
    m = FM_RE.match(text)
    if not m:
        raise RuntimeError(f"no frontmatter in {md_path}")
    fm_body = m.group(1)
    rest = text[m.end():]

    # build new fm_body line by line, replacing/adding keys
    lines = fm_body.splitlines()
    seen_keys: set[str] = set()
    new_lines: list[str] = []

    # also patch seed if it's null/empty
    seed_quoted = f'"{seed}"' if seed else "null"

    for line in lines:
        km = KV_RE.match(line)
        if km:
            key = km.group(1)
            if key == "seed":
                cur = strip_quotes(km.group(2))
                if cur in ("", "null") and seed:
                    new_lines.append(f"seed: {seed_quoted}")
                    seen_keys.add(key)
                    continue
            if key in stats:
                v = stats[key]
                new_lines.append(f"{key}: {format_yaml_value(v)}")
                seen_keys.add(key)
                continue
        new_lines.append(line)

    # append any stats keys not yet present
    appended = []
    for k in STATS_KEYS:
        if k not in seen_keys:
            appended.append(f"{k}: {format_yaml_value(stats[k])}")
    if appended:
        # marker comment so future readers know where stats start
        new_lines.append("# --- .run-derived stats (parse-run-history.py) ---")
        new_lines.extend(appended)

    new_text = "---\n" + "\n".join(new_lines) + "\n---" + rest
    md_path.write_text(new_text, encoding="utf-8", newline="\n")


def format_yaml_value(v) -> str:
    if isinstance(v, bool):
        return "true" if v else "false"
    if isinstance(v, (int, float)):
        return str(v)
    s = str(v)
    if s == "" or any(ch in s for ch in ":#") or s.lower() in ("null", "true", "false"):
        return f'"{s}"'
    return s


# ---- main -------------------------------------------------------------------

def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--run-id", help="single run_id to process")
    ap.add_argument("--all", action="store_true", help="process every run record")
    ap.add_argument("--write", action="store_true", help="archive .run and patch frontmatter")
    ap.add_argument("--dry-run", action="store_true", help="report only (default)")
    args = ap.parse_args()

    if not args.run_id and not args.all:
        ap.error("specify --run-id or --all")
    if not args.write:
        args.dry_run = True

    if not HISTORY_DIR.exists():
        print(f"ERROR: history dir not found: {HISTORY_DIR}", file=sys.stderr)
        return 2

    history = scan_history()
    print(f"scanned {len(history)} .run files in history")

    md_paths = sorted(RUNS_DIR.glob("*.md"))
    targets = []
    for p in md_paths:
        text = p.read_text(encoding="utf-8")
        fm = parse_frontmatter(text)
        if not fm or "run_id" not in fm:
            continue
        rid = strip_quotes(fm["run_id"])
        if args.run_id and rid != args.run_id:
            continue
        targets.append((p, fm, rid))

    if args.run_id and not targets:
        print(f"ERROR: no run record matches run_id={args.run_id}", file=sys.stderr)
        return 2

    print(f"processing {len(targets)} run record(s)\n")
    failures = 0
    for md_path, fm, rid in targets:
        rf, reason = match_run(fm, history)
        if rf is None:
            print(f"  {rid}: NO MATCH -- {reason}")
            failures += 1
            continue
        archived = RUNS_DIR / f"{rid}.run"
        already = archived.exists()
        stats = extract_stats(rf.path)
        print(f"  {rid}")
        print(f"    matched: {rf.path.name} ({reason})")
        print(f"    archived: {'EXISTS' if already else 'MISSING'} -> {archived.name}")
        print(f"    act_reached={stats['act_reached']}  floors={stats['total_floors']}  "
              f"picks={stats['total_card_picks']}  dmg={stats['total_damage_taken']}  "
              f"elites={stats['elites_fought']}  rests={stats['rests_taken']}  "
              f"killed_by={stats['killed_by']}")
        if args.write:
            shutil.copy2(rf.path, archived)
            patch_frontmatter(md_path, stats, rf.seed)
            print(f"    WROTE: archived .run + patched frontmatter")
        else:
            print(f"    (dry-run, no changes)")
    print(f"\ndone. {failures} failure(s).")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
