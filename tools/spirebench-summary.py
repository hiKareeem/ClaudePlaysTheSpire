#!/usr/bin/env python3
"""
SpireBench trial summary generator.

Reads the per-run artifacts produced by trial-v0 and emits:
  - docs/benchmark/charts/*.png  -- six chart families
  - docs/benchmark/trial-v0-summary.md -- markdown report with stats
    tables and embedded chart references

Inputs (all relative to repo root):
  docs/benchmark/runs.csv                   -- one row per run, 35 cols
  docs/benchmark/runs/<run_id>.md           -- run record markdown
  docs/benchmark/runs/<run_id>.jsonl        -- floor-history snapshot
                                              (copied from %APPDATA% by
                                              operator's teardown step 4)

The CSV is the authoritative summary table. The .md records are useful
narrative but are not parsed beyond reading the front-matter for cross-
checking. The .jsonl files are the per-floor telemetry and feed the
HP/gold/deck/death-heatmap charts.

Usage:
    python tools/spirebench-summary.py
    python tools/spirebench-summary.py --runs-dir docs/benchmark/runs \\
                                        --out docs/benchmark
    python tools/spirebench-summary.py --filter spec_version=trial-v0.1

Charts produced:
    1. hp_curve_overlay.png           -- HP% per floor, line per run,
                                          color-coded by model
    2. gold_deck_growth.png           -- two-panel: gold curve + deck
                                          size curve, line per run
    3. floor_reach_distribution.png   -- boxplot of final_floor by model
    4. death_heatmap.png              -- model x floor heatmap of deaths
    5. tokens_cost_per_floor.png      -- two-panel bar chart: tokens/floor
                                          and cost/floor by model
    6. character_difficulty.png       -- model x character grid: median
                                          final_floor cell-coloured

Exit codes:
    0 = success (report written, even if some charts skipped due to
        empty data)
    1 = unrecoverable error (missing runs.csv, malformed CSV)

Dependencies: pandas, matplotlib, numpy. seaborn optional.
"""
from __future__ import annotations

import argparse
import json
import sys
from collections import defaultdict
from pathlib import Path
from typing import Any

import matplotlib

matplotlib.use("Agg")  # headless: no display required
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd

# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------

# Stable per-model colors so the same model gets the same colour across
# all charts in the report. Mapping is intentionally explicit rather than
# relying on a colormap so reordering runs doesn't shuffle hues.
MODEL_COLORS: dict[str, str] = {
    "claude-opus-4.7":  "#cc7833",  # claude amber
    "gpt-5.5":          "#10a37f",  # openai green
    "gemini-3.1-pro":   "#4285f4",  # google blue
    "glm-5.1":          "#9b59b6",  # zai purple
    "deepseek-v3.5":    "#1e88e5",  # deepseek deep blue
}

CHARACTER_ORDER = ["IRONCLAD", "SILENT", "DEFECT", "REGENT", "NECROBINDER"]

# Act 1 is floors 1-17 in StS2. Charts default to this window unless
# data extends further (Act 2 / Act 3 / Heart). We auto-extend to
# the max floor seen across all runs.
DEFAULT_FLOOR_MAX = 17


# ---------------------------------------------------------------------------
# Loading
# ---------------------------------------------------------------------------


def load_runs_csv(path: Path) -> pd.DataFrame:
    """Load runs.csv. Coerces numeric columns; leaves strings untouched.

    The CSV is shared with humans (LibreOffice Calc) so we tolerate
    blank cells, stray whitespace, and the occasional trailing comma row.
    """
    if not path.exists():
        print(f"ERROR: {path} not found.", file=sys.stderr)
        sys.exit(1)

    df = pd.read_csv(path, dtype=str, keep_default_na=False, na_values=[""])

    numeric_cols = [
        "ascension", "duration_minutes", "command_count", "ipc_error_count",
        "stall_count", "death_floor", "victory_floor", "final_hp",
        "final_gold", "tokens_in", "tokens_out", "tokens_cache_read",
        "tokens_cache_write", "tokens_reasoning", "tokens_total",
        "cost_usd", "wall_seconds", "step_finish_count",
    ]
    for col in numeric_cols:
        if col in df.columns:
            df[col] = pd.to_numeric(df[col], errors="coerce")

    # Strip whitespace from string columns (humans editing CSV in
    # spreadsheet apps love to leak trailing spaces).
    for col in df.columns:
        if df[col].dtype == object:
            df[col] = df[col].astype(str).str.strip()
            df.loc[df[col] == "nan", col] = np.nan

    return df


def load_floor_history(path: Path) -> pd.DataFrame | None:
    """Load a single run's floor-history.jsonl. Returns None on failure."""
    if not path.exists():
        return None
    try:
        rows = [json.loads(line) for line in path.read_text().splitlines() if line.strip()]
    except json.JSONDecodeError as e:
        print(f"WARN: malformed JSONL in {path}: {e}", file=sys.stderr)
        return None
    if not rows:
        return None
    df = pd.DataFrame(rows)
    # Convert ISO8601 timestamps; harmless if column missing.
    if "t" in df.columns:
        df["t"] = pd.to_datetime(df["t"], utc=True, errors="coerce")
    return df


def load_all_floor_histories(
    runs_df: pd.DataFrame, runs_dir: Path
) -> dict[str, pd.DataFrame]:
    """Build {run_id: floor_history_df} for every run with a .jsonl file."""
    out: dict[str, pd.DataFrame] = {}
    for run_id in runs_df["run_id"].dropna().unique():
        fh = load_floor_history(runs_dir / f"{run_id}.jsonl")
        if fh is not None:
            # Annotate so cross-run plots can color by model/character.
            row = runs_df[runs_df["run_id"] == run_id].iloc[0]
            fh = fh.copy()
            fh["run_id"] = run_id
            fh["model"] = row.get("model", "unknown")
            fh["character"] = row.get("character", "unknown")
            out[run_id] = fh
    return out


def final_floor(row: pd.Series) -> float:
    """Resolve final_floor from death_floor or victory_floor.

    Returns NaN if neither is set (e.g. operator-halt with no recorded
    floor). Callers should drop NaN before plotting.
    """
    if pd.notna(row.get("victory_floor")):
        return float(row["victory_floor"])
    if pd.notna(row.get("death_floor")):
        return float(row["death_floor"])
    return float("nan")


# ---------------------------------------------------------------------------
# Charts
# ---------------------------------------------------------------------------


def _model_color(model: str) -> str:
    """Stable color lookup with a sane fallback for new/unknown models."""
    return MODEL_COLORS.get(model, "#888888")


def _floor_max(histories: dict[str, pd.DataFrame]) -> int:
    """Largest floor reached across all loaded histories, min 17."""
    if not histories:
        return DEFAULT_FLOOR_MAX
    return max(DEFAULT_FLOOR_MAX, int(max(fh["floor"].max() for fh in histories.values())))


def chart_hp_curve_overlay(
    histories: dict[str, pd.DataFrame], out_path: Path
) -> bool:
    """HP% per floor, one line per run, model-coloured."""
    if not histories:
        return False
    fig, ax = plt.subplots(figsize=(10, 6))
    for run_id, fh in histories.items():
        if "hp" not in fh.columns or "maxHp" not in fh.columns:
            continue
        # Guard against maxHp=0 rows (shouldn't happen but cheap to be safe).
        valid = fh["maxHp"] > 0
        if not valid.any():
            continue
        hp_pct = (fh.loc[valid, "hp"] / fh.loc[valid, "maxHp"]) * 100
        ax.plot(
            fh.loc[valid, "floor"], hp_pct,
            color=_model_color(fh["model"].iloc[0]),
            alpha=0.45, linewidth=1.3,
        )
    # Legend: one entry per model, not per run.
    seen_models = sorted({fh["model"].iloc[0] for fh in histories.values()})
    for m in seen_models:
        ax.plot([], [], color=_model_color(m), label=m, linewidth=2)
    ax.legend(loc="lower left", fontsize=9)
    ax.set_xlabel("Floor")
    ax.set_ylabel("HP %")
    ax.set_title("HP curve per run (trial-v0)")
    ax.set_ylim(0, 105)
    ax.set_xlim(1, _floor_max(histories))
    ax.grid(True, alpha=0.3)
    ax.axvline(17, color="red", linestyle=":", alpha=0.4, label="_nolegend_")
    ax.text(17, 5, "Act 1 boss", color="red", fontsize=8, alpha=0.7)
    fig.tight_layout()
    fig.savefig(out_path, dpi=120)
    plt.close(fig)
    return True


def chart_gold_deck_growth(
    histories: dict[str, pd.DataFrame], out_path: Path
) -> bool:
    """Two panels: gold curve + deck size curve."""
    if not histories:
        return False
    fig, (ax_gold, ax_deck) = plt.subplots(1, 2, figsize=(14, 5))
    for run_id, fh in histories.items():
        c = _model_color(fh["model"].iloc[0])
        if "gold" in fh.columns:
            ax_gold.plot(fh["floor"], fh["gold"], color=c, alpha=0.45, linewidth=1.3)
        if "deckSize" in fh.columns:
            ax_deck.plot(fh["floor"], fh["deckSize"], color=c, alpha=0.45, linewidth=1.3)
    seen_models = sorted({fh["model"].iloc[0] for fh in histories.values()})
    for m in seen_models:
        ax_gold.plot([], [], color=_model_color(m), label=m, linewidth=2)
    ax_gold.legend(loc="upper left", fontsize=9)
    ax_gold.set_xlabel("Floor"); ax_gold.set_ylabel("Gold")
    ax_gold.set_title("Gold over time")
    ax_gold.grid(True, alpha=0.3)
    ax_deck.set_xlabel("Floor"); ax_deck.set_ylabel("Deck size")
    ax_deck.set_title("Deck size over time")
    ax_deck.grid(True, alpha=0.3)
    fmax = _floor_max(histories)
    ax_gold.set_xlim(1, fmax)
    ax_deck.set_xlim(1, fmax)
    fig.tight_layout()
    fig.savefig(out_path, dpi=120)
    plt.close(fig)
    return True


def chart_floor_reach_distribution(df: pd.DataFrame, out_path: Path) -> bool:
    """Boxplot of final_floor by model, jittered scatter overlay."""
    df = df.copy()
    df["final_floor"] = df.apply(final_floor, axis=1)
    df = df.dropna(subset=["final_floor", "model"])
    if df.empty:
        return False

    models = sorted(df["model"].unique(), key=lambda m: list(MODEL_COLORS).index(m)
                    if m in MODEL_COLORS else 999)
    fig, ax = plt.subplots(figsize=(10, 6))
    data_per_model = [df[df["model"] == m]["final_floor"].values for m in models]
    bp = ax.boxplot(
        data_per_model, tick_labels=models, patch_artist=True,
        medianprops={"color": "black", "linewidth": 2},
    )
    for patch, m in zip(bp["boxes"], models):
        patch.set_facecolor(_model_color(m))
        patch.set_alpha(0.5)
    # Jittered points.
    rng = np.random.default_rng(42)
    for i, m in enumerate(models):
        ys = df[df["model"] == m]["final_floor"].values
        xs = rng.normal(i + 1, 0.08, size=len(ys))
        ax.scatter(xs, ys, color=_model_color(m), edgecolor="black", linewidth=0.5,
                   alpha=0.8, s=40, zorder=3)
    ax.axhline(17, color="red", linestyle=":", alpha=0.4)
    ax.text(len(models) + 0.4, 17, "Act 1 boss", color="red", fontsize=8, alpha=0.7,
            verticalalignment="center")
    ax.set_ylabel("Final floor reached")
    ax.set_title("Floor-reach distribution by model (trial-v0)")
    ax.grid(True, axis="y", alpha=0.3)
    plt.setp(ax.get_xticklabels(), rotation=15, ha="right")
    fig.tight_layout()
    fig.savefig(out_path, dpi=120)
    plt.close(fig)
    return True


def chart_death_heatmap(df: pd.DataFrame, out_path: Path) -> bool:
    """Heatmap: rows = models, cols = death floor, cell = death count."""
    deaths = df[df["halt_reason"] == "death"].copy()
    deaths = deaths.dropna(subset=["death_floor", "model"])
    if deaths.empty:
        return False

    models = sorted(deaths["model"].unique(),
                    key=lambda m: list(MODEL_COLORS).index(m) if m in MODEL_COLORS else 999)
    floor_max = max(DEFAULT_FLOOR_MAX, int(deaths["death_floor"].max()))
    grid = np.zeros((len(models), floor_max), dtype=int)
    for _, r in deaths.iterrows():
        mi = models.index(r["model"])
        fi = int(r["death_floor"]) - 1
        if 0 <= fi < floor_max:
            grid[mi, fi] += 1

    fig, ax = plt.subplots(figsize=(max(8, floor_max * 0.4), 1 + len(models) * 0.6))
    im = ax.imshow(grid, aspect="auto", cmap="Reds")
    ax.set_xticks(range(floor_max)); ax.set_xticklabels(range(1, floor_max + 1), fontsize=8)
    ax.set_yticks(range(len(models))); ax.set_yticklabels(models, fontsize=9)
    # Annotate cells with counts where >0.
    for mi in range(len(models)):
        for fi in range(floor_max):
            if grid[mi, fi] > 0:
                ax.text(fi, mi, str(grid[mi, fi]),
                        ha="center", va="center",
                        color="black" if grid[mi, fi] < grid.max() * 0.6 else "white",
                        fontsize=8)
    ax.axvline(16.5, color="red", linestyle=":", alpha=0.5)  # Act 1 boss boundary
    ax.set_xlabel("Death floor"); ax.set_title("Where do runs die? (trial-v0)")
    fig.colorbar(im, ax=ax, label="Death count")
    fig.tight_layout()
    fig.savefig(out_path, dpi=120)
    plt.close(fig)
    return True


def chart_tokens_cost_per_floor(df: pd.DataFrame, out_path: Path) -> bool:
    """Two-panel bar chart: median tokens/floor and median cost/floor by model."""
    df = df.copy()
    df["final_floor"] = df.apply(final_floor, axis=1)
    df = df.dropna(subset=["final_floor", "tokens_total", "model"])
    df = df[df["final_floor"] > 0]
    if df.empty:
        return False
    df["tokens_per_floor"] = df["tokens_total"] / df["final_floor"]
    if "cost_usd" in df.columns:
        df["cost_per_floor"] = df["cost_usd"] / df["final_floor"]

    models = sorted(df["model"].unique(),
                    key=lambda m: list(MODEL_COLORS).index(m) if m in MODEL_COLORS else 999)
    tok_med = [df[df["model"] == m]["tokens_per_floor"].median() for m in models]
    cost_med = [df[df["model"] == m]["cost_per_floor"].median() if "cost_per_floor" in df.columns else 0
                for m in models]

    fig, (ax_t, ax_c) = plt.subplots(1, 2, figsize=(14, 5))
    colors = [_model_color(m) for m in models]
    ax_t.bar(models, tok_med, color=colors, edgecolor="black")
    ax_t.set_ylabel("Tokens per floor (median)")
    ax_t.set_title("Token efficiency")
    ax_t.grid(True, axis="y", alpha=0.3)
    plt.setp(ax_t.get_xticklabels(), rotation=15, ha="right")

    ax_c.bar(models, cost_med, color=colors, edgecolor="black")
    ax_c.set_ylabel("Cost USD per floor (median)")
    ax_c.set_title("Cost efficiency")
    ax_c.grid(True, axis="y", alpha=0.3)
    plt.setp(ax_c.get_xticklabels(), rotation=15, ha="right")

    fig.tight_layout()
    fig.savefig(out_path, dpi=120)
    plt.close(fig)
    return True


def chart_character_difficulty(df: pd.DataFrame, out_path: Path) -> bool:
    """Model x character grid: median final_floor per cell."""
    df = df.copy()
    df["final_floor"] = df.apply(final_floor, axis=1)
    df = df.dropna(subset=["final_floor", "model", "character"])
    if df.empty:
        return False

    models = sorted(df["model"].unique(),
                    key=lambda m: list(MODEL_COLORS).index(m) if m in MODEL_COLORS else 999)
    chars = [c for c in CHARACTER_ORDER if c in df["character"].unique()]
    if not chars or not models:
        return False

    grid = np.full((len(models), len(chars)), np.nan)
    for mi, m in enumerate(models):
        for ci, c in enumerate(chars):
            sub = df[(df["model"] == m) & (df["character"] == c)]["final_floor"]
            if not sub.empty:
                grid[mi, ci] = sub.median()

    fig, ax = plt.subplots(figsize=(1.5 + len(chars) * 1.4, 1 + len(models) * 0.6))
    # Mask NaN cells with a neutral grey so they're visually distinct
    # from "0 floor" results.
    masked = np.ma.masked_invalid(grid)
    cmap = plt.cm.viridis.copy(); cmap.set_bad("#dddddd")
    im = ax.imshow(masked, aspect="auto", cmap=cmap, vmin=0)
    ax.set_xticks(range(len(chars))); ax.set_xticklabels(chars, rotation=20, ha="right", fontsize=9)
    ax.set_yticks(range(len(models))); ax.set_yticklabels(models, fontsize=9)
    for mi in range(len(models)):
        for ci in range(len(chars)):
            v = grid[mi, ci]
            if not np.isnan(v):
                ax.text(ci, mi, f"{v:.0f}",
                        ha="center", va="center",
                        color="white" if v < (np.nanmax(grid) * 0.5) else "black",
                        fontsize=10)
            else:
                ax.text(ci, mi, "—", ha="center", va="center", color="#888", fontsize=10)
    ax.set_title("Median final floor by model × character")
    fig.colorbar(im, ax=ax, label="Median final floor")
    fig.tight_layout()
    fig.savefig(out_path, dpi=120)
    plt.close(fig)
    return True


# ---------------------------------------------------------------------------
# Stats tables
# ---------------------------------------------------------------------------


def per_model_summary(df: pd.DataFrame) -> pd.DataFrame:
    """One row per model with aggregate stats."""
    df = df.copy()
    df["final_floor"] = df.apply(final_floor, axis=1)
    rows: list[dict[str, Any]] = []
    for model, sub in df.groupby("model"):
        n = len(sub)
        wins = (sub["halt_reason"] == "victory").sum()
        deaths = (sub["halt_reason"] == "death").sum()
        rate_limits = (sub["halt_reason"] == "rate_limit").sum()
        floors = sub["final_floor"].dropna()
        rows.append({
            "model": model,
            "runs": n,
            "victories": int(wins),
            "deaths": int(deaths),
            "rate_limits": int(rate_limits),
            "median_floor": float(floors.median()) if not floors.empty else None,
            "max_floor": float(floors.max()) if not floors.empty else None,
            "mean_tokens_total": float(sub["tokens_total"].mean()) if "tokens_total" in sub else None,
            "mean_cost_usd": float(sub["cost_usd"].mean()) if "cost_usd" in sub else None,
            "mean_wall_seconds": float(sub["wall_seconds"].mean()) if "wall_seconds" in sub else None,
        })
    out = pd.DataFrame(rows)
    if out.empty:
        return out
    return out.sort_values("median_floor", ascending=False, na_position="last")


def per_character_summary(df: pd.DataFrame) -> pd.DataFrame:
    """One row per character with aggregate stats."""
    df = df.copy()
    df["final_floor"] = df.apply(final_floor, axis=1)
    rows: list[dict[str, Any]] = []
    for char, sub in df.groupby("character"):
        floors = sub["final_floor"].dropna()
        rows.append({
            "character": char,
            "runs": len(sub),
            "victories": int((sub["halt_reason"] == "victory").sum()),
            "median_floor": float(floors.median()) if not floors.empty else None,
            "max_floor": float(floors.max()) if not floors.empty else None,
        })
    out = pd.DataFrame(rows)
    if out.empty:
        return out
    sort_key = {c: i for i, c in enumerate(CHARACTER_ORDER)}
    return out.sort_values("character", key=lambda s: s.map(lambda c: sort_key.get(c, 999)))


def df_to_md(df: pd.DataFrame) -> str:
    """Render a DataFrame as a GitHub-flavoured markdown table.

    pandas.to_markdown requires tabulate; do it by hand to avoid the dep.
    """
    if df.empty:
        return "_(no rows)_\n"
    cols = list(df.columns)
    out = ["| " + " | ".join(cols) + " |", "| " + " | ".join("---" for _ in cols) + " |"]
    for _, row in df.iterrows():
        cells = []
        for c in cols:
            v = row[c]
            if pd.isna(v):
                cells.append("—")
            elif isinstance(v, float):
                cells.append(f"{v:.2f}" if abs(v) < 1000 else f"{v:.0f}")
            else:
                cells.append(str(v))
        out.append("| " + " | ".join(cells) + " |")
    return "\n".join(out) + "\n"


# ---------------------------------------------------------------------------
# Report
# ---------------------------------------------------------------------------


def write_report(
    df: pd.DataFrame, charts_emitted: dict[str, bool],
    charts_dir: Path, out_md: Path, spec_version: str,
) -> None:
    """Compose the final markdown report."""
    n_runs = len(df)
    n_with_record = df["run_id"].notna().sum()
    halt_counts = df["halt_reason"].value_counts().to_dict() if "halt_reason" in df else {}

    lines: list[str] = []
    lines.append(f"# SpireBench {spec_version} — Trial Summary")
    lines.append("")
    lines.append("_Auto-generated by `tools/spirebench-summary.py`. Do not edit by hand —")
    lines.append("re-run the script after appending new runs to `runs.csv`._")
    lines.append("")
    lines.append("## Overview")
    lines.append("")
    lines.append(f"- Total runs: **{n_runs}** (with run records: {n_with_record})")
    if halt_counts:
        parts = [f"{k}={v}" for k, v in sorted(halt_counts.items())]
        lines.append(f"- Halt reasons: {', '.join(parts)}")
    if "model" in df:
        models = sorted(df["model"].dropna().unique())
        lines.append(f"- Models tested: {', '.join(models)}")
    if "character" in df:
        chars = sorted(df["character"].dropna().unique())
        lines.append(f"- Characters: {', '.join(chars)}")
    lines.append("")

    lines.append("## Per-model results")
    lines.append("")
    lines.append(df_to_md(per_model_summary(df)))
    lines.append("")

    lines.append("## Per-character results")
    lines.append("")
    lines.append(df_to_md(per_character_summary(df)))
    lines.append("")

    lines.append("## Charts")
    lines.append("")
    chart_titles = {
        "hp_curve_overlay": "HP curve overlay",
        "gold_deck_growth": "Gold and deck-size growth",
        "floor_reach_distribution": "Floor-reach distribution",
        "death_heatmap": "Death-floor heatmap",
        "tokens_cost_per_floor": "Tokens and cost per floor",
        "character_difficulty": "Character difficulty matrix",
    }
    for key, title in chart_titles.items():
        if charts_emitted.get(key, False):
            rel = f"charts/{key}.png"
            lines.append(f"### {title}")
            lines.append("")
            lines.append(f"![{title}]({rel})")
            lines.append("")
        else:
            lines.append(f"### {title}")
            lines.append("")
            lines.append("_Insufficient data — chart skipped._")
            lines.append("")

    out_md.write_text("\n".join(lines), encoding="utf-8")


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--runs-csv", default="docs/benchmark/runs.csv", type=Path)
    parser.add_argument("--runs-dir", default="docs/benchmark/runs", type=Path)
    parser.add_argument("--out", default="docs/benchmark", type=Path,
                        help="Output dir for trial-v0-summary.md and charts/")
    parser.add_argument("--filter", action="append", default=[],
                        metavar="KEY=VALUE",
                        help="Filter runs (e.g. --filter spec_version=trial-v0.1). Repeatable.")
    parser.add_argument("--spec-version", default="trial-v0.1",
                        help="Used in the report title.")
    args = parser.parse_args()

    df = load_runs_csv(args.runs_csv)

    for f in args.filter:
        if "=" not in f:
            print(f"WARN: ignoring malformed --filter {f!r} (need KEY=VALUE)", file=sys.stderr)
            continue
        k, v = f.split("=", 1)
        if k not in df.columns:
            print(f"WARN: --filter {k}=... refers to unknown column", file=sys.stderr)
            continue
        df = df[df[k] == v]

    if df.empty:
        print("WARN: no runs matched filters; report will contain empty tables.", file=sys.stderr)

    histories = load_all_floor_histories(df, args.runs_dir)
    print(f"Loaded {len(df)} run(s); {len(histories)} have floor-history.")

    charts_dir = args.out / "charts"
    charts_dir.mkdir(parents=True, exist_ok=True)

    emitted = {
        "hp_curve_overlay":         chart_hp_curve_overlay(histories, charts_dir / "hp_curve_overlay.png"),
        "gold_deck_growth":         chart_gold_deck_growth(histories, charts_dir / "gold_deck_growth.png"),
        "floor_reach_distribution": chart_floor_reach_distribution(df, charts_dir / "floor_reach_distribution.png"),
        "death_heatmap":            chart_death_heatmap(df, charts_dir / "death_heatmap.png"),
        "tokens_cost_per_floor":    chart_tokens_cost_per_floor(df, charts_dir / "tokens_cost_per_floor.png"),
        "character_difficulty":     chart_character_difficulty(df, charts_dir / "character_difficulty.png"),
    }
    for k, ok in emitted.items():
        print(f"  chart: {k:30s} {'OK' if ok else 'SKIPPED (no data)'}")

    out_md = args.out / "trial-v0-summary.md"
    write_report(df, emitted, charts_dir, out_md, args.spec_version)
    print(f"Wrote {out_md}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
