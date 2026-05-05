# SpireBench Protocol — Trial v1 (Draft)

> **Status:** Draft. Successor to `protocol.md` (trial-v0). Freezes once v0 wraps and v1 first run starts. **Trial-v0 records remain governed by `protocol.md`** — v0 is the closed control set.
>
> v1 supersedes v0; runs from different trials are **not** comparable except where noted.

**Spec version:** `trial-v1`
**Bridge version target:** v0.2.0 (or successor — see `Bridge build` in §Run record)
**Game version:** Slay the Spire 2 v0.104.0+
**Trial freeze date:** TBD (set on v1 first-run kickoff)
**Knowledge conditions:** `A0-zero-shot`, `B0-priors` (see §Knowledge conditions)

---

## What changed from trial-v0

Source: `docs/benchmark/trial-v0-findings-audit.md`. Full rationale lives there.

| Area | v0 | v1 | Why |
|---|---|---|---|
| Knowledge conditions | A0 only | A0 + B0 | Test whether a fixed priors doc closes combat-misplay deaths and matches Opus's docs-checking discipline. |
| Cell replication | k=1 | k=3 | v0 within-model variance was 30+ floors (run10 vs run11). k=1 is too noisy. |
| Seed strategy | Unseeded | **3 fixed seeds, paired across all cells** | Paired comparison ≈ doubles statistical power for model-vs-model. |
| Run-cap (commands) | 500 | 1000 | Ironclad gpt-5.5 reached F50 in ~290 cmds; depth runs need headroom. |
| Halt taxonomy | death, victory, runcap, error_streak, stall, rate_limit, manual | + `state_inconsistent`, + `budget_exhausted` | v0 stalls on 0-available-nodes were not really agent failure. |
| Composite score | None (descriptive) | `floors + 10·act + 50·victory − 0.1·errors` | Single comparable scalar across cells. |
| Pre-flight | Manual checklist | Automated DLL `modVersion` check | Run18 lost to stale Steam DLL. |
| Bridge `state_version` | Implicit revision counter | Explicit `state_version` int on every response | Lets agent detect staleness deterministically. |
| Bridge `force_refresh` verb | Absent | New IPC verb | Let agent recover from `state_inconsistent` once before halt. |

Trial scale: 5 models × 5 characters × 2 conditions × k=3 seeds = **150 runs** (vs v0's 25).

If 150 is too expensive, fallback ladder (in priority order to drop):
1. Drop B0 → 75 runs (loses the priors hypothesis, keeps paired-3-seeds variance characterization).
2. Drop k=3 to k=2 → 100 runs (halves variance budget but still paired).
3. Drop one model → 120 runs.

Recommend committing to 150 unless the v0 cost data says otherwise.

---

## Knowledge conditions

### A0-zero-shot (control, unchanged from v0)

No prior-run context, no persistent memory across runs, no web search, no training-recall fallback. Reading list per §Allowed reading.

### B0-priors (new in v1)

Identical to A0 **except** the agent is required to read one additional file before play:

- `docs/benchmark/priors.md` — frozen for the duration of v1, single hand-curated rules sheet.

`priors.md` content scope (frozen by v1 freeze date):
- Combat fundamentals: block density, scaling-vs-burst trade-off, energy curve.
- Pre-elite / pre-boss checklist: "before each elite or act boss, read the relevant `docs/data/eng/encounters_*.json` entry."
- Map literacy: when to skip an elite, when to take rest, deck-thinning thresholds.
- Character-specific notes (one paragraph each, no card-pick recommendations — that crosses into strategy contamination).

`priors.md` is **frozen at v1 freeze date**. Edits invalidate prior B0 runs (re-tag `incomparable`, do not silently overwrite).

The B0 condition makes the *implicit* tool-use discipline observed in Opus 4.7 (consistent docs-json checking) into an *explicit* instruction the weaker models can follow. Hypothesis: B0 closes ~half of combat-misplay deaths in mid-tier models.

A0 and B0 records carry the same schema; the `knowledge_condition` field distinguishes them.

---

## Seed strategy (paired-3-seeds)

Three fixed seeds chosen at v1 freeze date: `seed_alpha`, `seed_beta`, `seed_gamma`.

Every cell `(model, character, knowledge_condition)` runs all three seeds. Total runs per cell = 3.

Rationale:
- **Paired comparison** between any two models (or any two conditions) uses the same three seeds, isolating cell-level effect from seed-level variance.
- Statistical analysis is a paired test (Wilcoxon signed-rank for floor-reached, McNemar for victory). Power ≈ 2× unpaired.
- Reproducibility: any cell can be re-run on the same seeds for spot-checks.

Seed selection criteria: pick three seeds that, when seed-replayed manually, produce diverse Act 1 starts (not all easy, not all curse-Neow). Pre-screen by running them through one Opus 4.7 control pass per character before freeze.

Record the three seeds in `priors.md` is forbidden (would leak per-seed knowledge); record them in `docs/benchmark/seeds-v1.md` (operator-only, not in agent reading list).

---

## Trial parameters

| Axis | Trial-v1 values |
|---|---|
| **Character** | `IRONCLAD`, `SILENT`, `DEFECT`, `REGENT`, `NECROBINDER` |
| **Ascension** | `0` (only — defer A1 to v2; gate on v1 A0 winrate ≥20%) |
| **Knowledge condition** | `A0-zero-shot`, `B0-priors` |
| **Seed** | One of `seed_alpha`, `seed_beta`, `seed_gamma` (paired across all cells) |
| **Run-cap (commands)** | `1000` |
| **Stall thresholds** | `bridge_stall`: 30s no revision change. `agent_stall`: 120s no `step_finish`. Both halt as `halt_reason: stall`. `rate_limit_pause` (non-terminal) carries over from v0.4 unchanged. |
| **Halt conditions** | `GameOver`, `Victory`, run-cap reached, 5 consecutive `status=error` on distinct commands, stall, terminal rate-limit, **state-inconsistent (after one `force_refresh`)**, **budget-exhausted (operator cap)**, manual abort |
| **Mid-run model rotation** | Prohibited (unchanged). |
| **Session isolation** | One fresh OpenCode session per run (unchanged). Pre-flight script enforces. |

Models under evaluation in trial-v1 (same lineup as v0 unless v0 results force a swap):

- `claude-opus-4.7` (frontier control, closed-weights)
- `gpt-5.5` (frontier, closed-weights)
- `gemini-3.1-pro` (frontier, closed-weights)
- `glm-5.1` (frontier, open-weights)
- `deepseek-v3.5` (frontier, open-weights, free tier)

---

## Halt taxonomy (v1 additions)

Existing v0 reasons (`death`, `victory`, `runcap`, `error_streak`, `stall`, `rate_limit`, `manual`) all carry over.

New in v1:

- `state_inconsistent` — bridge reports impossible state (e.g. `available[]` empty on a non-boss floor) and one `force_refresh` failed to recover. Distinct from `stall` (which is time-based).
- `budget_exhausted` — operator-set time cap, token cap, or USD cap hit. Distinct from `runcap` (commands) and `rate_limit` (provider-side).

Death-cause taxonomy unchanged from v0.

---

## Run record (v1 schema)

Schema delta from v0:

```yaml
spec_version: trial-v1               # was trial-v0.x
bridge_version: v0.2.0               # bumped
knowledge_condition: B0-priors       # A0-zero-shot | B0-priors
seed: seed_alpha                     # one of three fixed seeds (string, not numeric)
seed_label: alpha                    # alpha | beta | gamma — for sorting/grouping
priors_version: v1.0                 # only set if knowledge_condition=B0-priors; matches priors.md frozen version
composite_score: 47.3                # = floor + 10*act + 50*win - 0.1*errors
state_version_max: 1247              # max state_version observed during run (v1 bridge field)
force_refresh_count: 0               # number of times agent invoked force_refresh
halt_reason: state_inconsistent      # extended taxonomy
```

All other v0 fields retained. v0 and v1 records share the same field names where they overlap; the `spec_version` field disambiguates schema generation.

---

## Bridge changes required (v0.2.0)

For the protocol to function, the bridge must add:

1. **`state_version` field** on every state response. Monotonic, increments on every state mutation. Distinct from existing `revision` (which is file-write counter).
2. **`force_refresh` IPC verb.** Re-reads game-canonical state and emits a fresh state.json. Idempotent. Returns `{status: ok|error, state: {...}, refreshed: bool}`.
3. **Pre-flight DLL version check.** Bridge logs its `modVersion` to `trace.log` on startup; pre-flight script reads it and aborts if stale.
4. **`state_inconsistent` event.** When bridge detects `available[]` empty on non-boss floor for 2 consecutive ticks, emit a structured event. Does not auto-halt — agent still has the option to `force_refresh`.

These are additive. v0 records remain readable.

---

## Operator pre-flight (v1 additions)

Add to v0 pre-flight checklist:

7. **DLL `modVersion` matches repo HEAD.** Run `tools/preflight-dll-version.ps1` (new). Aborts with non-zero exit if stale. Manual check: `Get-Content (Join-Path $env:APPDATA 'SlayTheSpire2\hermesbridge\trace.log') | Select-String 'modVersion'` matches `git rev-parse HEAD` short prefix.
8. **Seed pre-loaded.** For paired-seeds trial, the seed is set via game UI or save-edit before the agent's first command. The agent does **not** know which seed it is on (seed value is not in agent reading list); the operator records it in the run record post-run.
9. **`opencode.benchmark.json` is the active config.** Lints pre-flight (run11 was contaminated by maintainer config).

---

## Tool exposure (unchanged from v0.4)

`mempalace_*`, web fetch / search, sub-agent spawning, MCP servers other than the bridge, reading outside the working directory — all forbidden. Operator pre-flight enforces via sandboxed `opencode.benchmark.json`.

Run11 protocol violation pattern (tools exposed but unused) is acceptable in v0; in v1, exposure alone is a pre-flight failure and the run must be re-attempted.

---

## Scoring

**Primary metric (v1):** `composite_score = floor_reached + 10·act_reached + 50·victory − 0.1·ipc_error_count`

- Rewards depth (each act-clear worth +10).
- Strongly rewards victory (+50 to break ties between deep losses and wins).
- Mild error penalty (errors don't disqualify a run, but excessive errors cost ~5–10 points).

**Per-model reporting (v1):**
- Mean composite_score across 15 cells (5 chars × 3 seeds × 2 conditions).
- A0 vs B0 paired comparison: per-cell `composite_score(B0) − composite_score(A0)`, Wilcoxon signed-rank.
- Closed-vs-open paired comparison.
- Death-cause histogram, IPC error rate, stall rate (descriptive, unchanged from v0).
- Within-model variance: range and IQR of `composite_score` across the 3 seeds per cell.

A model "passes Act 1" if its A0 mean composite_score ≥ 17 (= reach Act 1 boss without victory). Threshold for trial-v1 admission to v2 is mean A0 composite_score ≥ 30.

---

## Trial freeze and amendment policy

This document freezes at v1 first-run kickoff. Same amendment rules as v0:

- Sub-versions tagged `trial-v1.N`.
- Schema-affecting changes invalidate prior runs (re-tag `incomparable`).
- Workflow-only changes do not bump `spec_version`.

**`priors.md` is frozen with this protocol.** Editing `priors.md` mid-trial invalidates all prior B0 runs.

---

## Open items before v1 freeze

These must be resolved before kickoff:

1. **Pick the three seeds.** Operator-side; needs one Opus 4.7 control pass per character to confirm diversity.
2. **Write `priors.md`.** Source: v0 audit §4 + Opus's behavioral patterns. ~3–5 pages, character-neutral fundamentals + one paragraph per character.
3. **Bridge v0.2.0.** Implement `state_version`, `force_refresh`, `state_inconsistent` event, DLL pre-flight log line.
4. **Composite score weights.** Calibrate against v0 data: do the proposed weights produce the run-ranking we'd produce by hand? If not, tune before freeze.
5. **Run-cap bump confirmation.** Default 500 → 1000 in bridge; operator-side cap also raised.
6. **`tools/preflight-dll-version.ps1`.** New helper.
7. **Tool-leakage audit.** Add `preflight_screen` column to runs.csv to track per-run pre-flight state.

---

Source documents:
- `docs/benchmark/protocol.md` — trial-v0 (predecessor).
- `docs/benchmark/trial-v0-findings-audit.md` — empirical justification for every v1 delta above.
- `docs/benchmark/agent-prompt.md` — operator-pasted run preamble; will get a v1 variant.
