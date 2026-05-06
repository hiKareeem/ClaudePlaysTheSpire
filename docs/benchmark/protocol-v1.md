# SpireBench Protocol — Trial v1 (Draft)

> **Status:** Draft. Successor to `protocol.md` (trial-v0). Freezes once v0 wraps and v1 first run starts. **Trial-v0 records remain governed by `protocol.md`** — v0 is the closed control set.
>
> v1 supersedes v0; runs from different trials are **not** comparable except where noted.

**Spec version:** `trial-v1`
**Bridge version target:** SpireBridge v0.2.0 (or successor — see `Bridge build` in §Run record)
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
| Seed strategy | Unseeded | **3 fixed seeds, paired across all cells, agent-supplied via `StartRun`** | Paired comparison ≈ doubles statistical power for model-vs-model. Pre-loading via save-edit was unsound; the seed travels in the operator preamble and the agent forwards it on its first command. |
| Run-cap (commands) | 500 | 1000 | Ironclad gpt-5.5 reached F50 in ~290 cmds; depth runs need headroom. |
| Halt taxonomy | death, victory, runcap, error_streak, stall, rate_limit, manual | + `state_inconsistent`, + `budget_exhausted` | v0 stalls on 0-available-nodes were not really agent failure. |
| Composite score | None (descriptive) | `floors + 10·act + 50·victory − 0.1·errors` | Single comparable scalar across cells. |
| Pre-flight | Manual checklist | Automated DLL `modVersion` + bridge-version check | Run18 lost to stale Steam DLL; pre-v0.1.5 bridges report wrong `modVersion` (hardcoded `0.1.1` literal). |
| Bridge `stateVersion` | Implicit revision counter | Explicit `stateVersion` int on every state.json write | Lets agent detect staleness deterministically. |
| Bridge `force_refresh` verb | Absent | New IPC verb | Let agent recover from `state_inconsistent` once before halt. |
| Bridge brand | HermesBridge | **SpireBridge** v0.2.0 | Project no longer locked to a single integration host; rebrand follows the Python port. v0 records keep their `HermesBridge` build strings; v1 records emit `SpireBridge`. |
| Multi-instance | `HERMES_IPC_DIR` + `hermes-instance.cfg` (per `BridgePaths.cs`) | Same, plus `SPIREBRIDGE_IPC_DIR` alias and a pre-flight check that confirms each session targets a distinct IPC root | Concurrent benchmark instances are first-class; cross-talk would corrupt paired-seed comparisons silently. |
| Character-resource read | Some fields (`combat.stars`, `combat.orbs`) emitted but not surfaced by `tools/read-combat.ps1`; agents needed a second reflection dump | Every per-character combat resource is at a documented stable path under `combat`, surfaced by the v1 helper script in a single read | Weaker models (e.g. glm-5.1) consistently skipped the second read and flew blind on Regent / Defect. Closes one full class of "tooling-induced misplay." |

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

- `docs/benchmark/priors-<CHARACTER>.md` — frozen for the duration of v1, hand-curated rules sheet for the agent's assigned character (one of `priors-IRONCLAD.md`, `priors-SILENT.md`, `priors-DEFECT.md`, `priors-REGENT.md`, `priors-NECROBINDER.md`). The agent reads **only** the file for its assigned character; the other four are out-of-whitelist.

`priors-<CHARACTER>.md` content scope (frozen by v1 freeze date, identical structure across all five files):
- Combat fundamentals: block density, scaling-vs-burst trade-off, energy curve.
- Pre-elite / pre-boss checklist: "before each elite or act boss, read the relevant `docs/data/eng/encounters_*.json` entry."
- Map literacy: when to skip an elite, when to take rest, deck-thinning thresholds.
- Character-specific notes (one paragraph, no card-pick recommendations — that crosses into strategy contamination). Cross-character text is tuned to the assigned character (e.g. only Defect's file documents the orb queue in detail; Ironclad's file mentions other characters' resources only by reference).

`priors-<CHARACTER>.md` is **frozen at v1 freeze date**. Edits to any of the five files invalidate prior B0 runs of the affected character (re-tag `incomparable`, do not silently overwrite).

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

### Seed delivery

The seed cannot be pre-loaded into a save before the agent boots — StS2 does not expose a save-time seed-set we control reliably, and the previous v1 draft's "seed pre-loaded" assumption was unsound.

Instead, the agent receives the seed as a parameter on its very first `StartRun` command in the operator-pasted preamble. Concretely the preamble contains:

```
Your character is: <CHARACTER>
Your run seed is:  <SEED_STRING>
```

The agent's first action is `send-cmd StartRun { character: <CHARACTER>, seed: <SEED_STRING> }`. The bridge passes the seed straight to `NGame.Instance.StartNewSingleplayerRun`. SpireBridge v0.2.0 must accept and forward this parameter; the v0 bridge ignores `seed` silently, which would corrupt v1 data — the bridge preflight (§Operator pre-flight item 7) verifies the bridge is ≥v0.2.0.

The agent **is told its seed** (it appears in the preamble) but is **not told the seed-to-Act-1 mapping** — `seeds-v1.md` lives operator-only and is excluded from the agent reading list. The seed string is opaque to the agent; per-seed knowledge cannot leak between runs because A0/B0 conditions forbid persistent memory across sessions.

Recording: operator records the assigned seed in the run record (it's in the preamble; no post-run reconstruction needed).

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
- `deepseek-v4-pro` (frontier, open-weights, free tier)

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
bridge_version: SpireBridge-0.2.0    # bumped + rebranded
knowledge_condition: B0-priors       # A0-zero-shot | B0-priors
seed: seed_alpha                     # one of three fixed seeds (string, not numeric)
seed_label: alpha                    # alpha | beta | gamma — for sorting/grouping
priors_version: v1.0                 # only set if knowledge_condition=B0-priors; matches priors-<CHARACTER>.md frozen version
composite_score: 47.3                # = floor + 10*act + 50*win - 0.1*errors
state_version_max: 1247              # max stateVersion observed during run (v1 bridge field, JSON key `stateVersion`)
force_refresh_count: 0               # number of times agent invoked ForceRefresh command
halt_reason: state_inconsistent      # extended taxonomy
```

All other v0 fields retained. v0 and v1 records share the same field names where they overlap; the `spec_version` field disambiguates schema generation.

---

## Bridge changes required (SpireBridge v0.2.0)

For the protocol to function, the bridge must add:

1. **`stateVersion` field — landed in v0.2.0.** Emitted on every state.json write under JSON key `stateVersion` (camelCase, matching sibling fields). Monotonic int, advances by 1 per `BridgeSnapshotWriter.RequestWrite` call. In current v0.2.0 implementation `stateVersion == revision` always, because both counters are incremented before the optional unchanged-payload skip (which is dead code — both fresh counters in the JSON guarantee the equality check fails). The contract distinction is preserved for forward compatibility: `revision` documents bridge-internal write sequence; `stateVersion` is the agent-facing staleness key. v1 agents poll `stateVersion -gt $afterStateVersion` (or `revision`, equivalent today) to detect state updates.
2. **`ForceRefresh` IPC verb — landed in v0.2.0.** Command type `ForceRefresh` (no parameters) re-pulls every snapshot slot from canonical game state by calling each `BridgeSingleton.PushCurrent*` method (Run, Combat, Event, Shop, RestSite, Treasure, Map). Each Push self-skips when its room/state isn't active, so the call is safe on any screen. The bridge's standard post-dispatch `RequestWrite` then flushes the rebuilt slots to state.json, advancing `stateVersion` and `revision`. Idempotent. Result: `{status: "ok", message: "force-refresh complete; new stateVersion will be > N"}`. Agents detect completion by polling `stateVersion -gt $beforeStateVersion` after the result lands. The "state" payload is NOT inlined into the result (state.json is the single source of truth, so result-side state would only duplicate ~50 KB per refresh and risk staleness if agents relied on the result copy instead of re-reading state.json).
3. **Pre-flight DLL version check.** Bridge logs its `modVersion` to `trace.log` on startup; pre-flight script reads it and aborts if stale.
4. **`state_inconsistent` event.** When bridge detects `available[]` empty on non-boss floor for 2 consecutive ticks, emit a structured event. Does not auto-halt — agent still has the option to `force_refresh`.
5. **`StartRun.seed` parameter honored — verified, no change needed.** Audit of `BridgeCommandDispatcher.DispatchStartRun` (HermesBridgeCode/BridgeCommandDispatcher.cs:1511) confirms v0.1.5 already reads `seed` from the JSON command body and forwards it to `NGame.Instance.StartNewSingleplayerRun(character, shouldSave:true, acts:ModelDb.Acts, modifiers:[], seed:seed, gameMode, ascensionLevel, dailyTime:null)`. Empty seed is auto-synthesized as a random 64-bit decimal string. Earlier draft of this document claimed v0 silently dropped the seed; that was incorrect (it predated v0.1.5's seed plumbing). Kept as a numbered entry so the contract — "the bridge accepts an agent-supplied seed in `StartRun` and forwards it" — remains explicit and tested in v1 pre-flight.
6. **Character-specific combat resources in initial `combat` payload.** A single `Read-State` must surface every character-specific combat resource the agent could need to make the next decision: `combat.stars` (Regent), `combat.orbs` + `combat.orbCapacity` (Defect), and Osty as an ally entry under `combat.allies[]` (Necrobinder — Osty is the entire mechanic; soaks unblocked damage before the player does; no separate player-side meter exists). v0 surfaces stars/orbs at the combat root but the v0 helper script (`tools/read-combat.ps1`) did not display them, so weaker agents who never did a follow-up reflection dump flew blind. The bridge contract for v1 is: every character-distinguishing resource is at a stable, documented, top-level path under `combat`, and the v1 helper script must display it. Implementation must enumerate per-character resource paths in the v0.2.0 release notes.
7. **Multi-instance support (carry-over, formalized).** v0 already supports concurrent StS2 instances via `HERMES_IPC_DIR` env-var override and `hermes-instance.cfg` (per `BridgePaths.cs`, sanitized instance ids `[A-Za-z0-9_-]+`). v1 keeps this first-class. **Landed in v0.2.0:** the env var is now `SPIREBRIDGE_IPC_DIR` in line with the rebrand; `HERMES_IPC_DIR` remains honored as a deprecated alias (with a one-line deprecation diagnostic on each load), and when both are set `SPIREBRIDGE_IPC_DIR` wins. The deprecated alias will be removed in a future major version. The startup diagnostic line continues to name the active IPC root (preserve format).
8. **No `CancelTargeting` command — by design.** Run22 attempted a `CancelTargeting` command and got back `unknown command type: CancelTargeting`. On audit, this is correct behavior, not a gap. The bridge's `PlayCard` dispatch invokes `card.TryManualPlay(target)` synchronously, passing the target directly to the play API — the targeting UI is never entered through the bridge, so there is no "pending target prompt" state for the bridge to cancel. A wedged target prompt is only reachable via a stray manual mouse click (out-of-band) or via state-read misinterpretation, neither of which the bridge can or should resolve. v1 agents are instructed to: (a) treat a `false` return from `TryManualPlay` as a card-unplayable signal (re-read state, pick a different action) rather than as evidence of a stuck UI; (b) never need a cancel verb. This entry is preserved here so future readers know the omission was deliberated.

These are additive. v0 records remain readable, but v1 records require a v0.2.0+ bridge.

---

## Operator pre-flight (v1 additions)

Add to v0 pre-flight checklist:

7. **DLL `modVersion` matches repo HEAD and is ≥v0.2.0.** Run `tools/preflight-dll-version.ps1` (new). Aborts with non-zero exit if the bridge reports anything other than the expected v0.2.0+ build. Manual check: `Get-Content (Join-Path $env:APPDATA 'SlayTheSpire2\hermesbridge\trace.log') | Select-String 'modVersion\|version='` shows `version=0.2.0` (or later) on the most-recent `Initialize start` line. Pre-v0.1.5 bridges report a stale `modVersion=0.1.1` (literal was hardcoded; fixed in v0.2.0 by reading from `MainFile.BridgeVersion`).
8. **Seed assigned to this run.** Operator picks the seed for this cell (one of `seed_alpha` / `seed_beta` / `seed_gamma` from `docs/benchmark/seeds-v1.md`, operator-only). The seed string is written into the operator-pasted run preamble alongside the character. The agent forwards it on its first `StartRun` command; the bridge applies it. The agent does not see `seeds-v1.md` and so cannot use per-seed knowledge across runs (A0/B0 forbid persistent memory anyway).
9. **`opencode.benchmark.json` is the active config.** Lints pre-flight (run11 was contaminated by maintainer config).
10. **Multi-instance isolation (if running concurrent benchmark instances).** Each StS2 instance runs against a separate `HERMES_IPC_DIR` (or successor `SPIREBRIDGE_IPC_DIR` in the v0.2.0 rename — both honored, see §Bridge changes). Bridge writes a startup diagnostic naming the active IPC root; pre-flight script reads that line and confirms each agent session targets a distinct directory. Cross-talk between concurrent runs is a `state_inconsistent` failure mode and disqualifies both records.

---

## Tool exposure (unchanged from v0.4)

`mempalace_*`, web fetch / search, sub-agent spawning, MCP servers other than the bridge, reading outside the working directory — all forbidden. Operator pre-flight enforces via sandboxed `opencode.benchmark.json`.

Run11 protocol violation pattern (tools exposed but unused) is acceptable in v0; in v1, exposure alone is a pre-flight failure and the run must be re-attempted.

---

## Scoring

**Primary metric (v1):** `composite_score = floor_reached + 10·act_reached + 50·victory − 0.1·ipc_error_count`

- Rewards depth (each act-clear worth +10).
- Strongly rewards victory (+50 to break ties between deep losses and wins).
- Mild error penalty (errors don't disqualify a run, but excessive errors cost ~2–5 points).

**Calibration against v0 data (n=22).** Verified via `tools/calibrate-composite.py` against the v0 runs.csv. Selected results:

| Rank | Score | Run | Halt | Floor | Act | Errors | Notes |
|---:|---:|:---|:---|---:|---:|---:|:---|
| 1 | 79.4 | run11 gpt-5.5 IRONCLAD | death | 50 | 3 | 6 | Deepest run; only Act-3 reach. |
| 2 | 47.4 | run12 gpt-5.5 SILENT | death | 28 | 2 | 6 | Decimillipede elite kill. |
| 3 | 41.2 | run19 deepseek-v4-pro NECRO | death | 23 | 2 | 18 | Act-2 progress. |
| 6 | 33.8 | run21 claude-opus-4.7 IRONC | death | 14 | 2 | 2 | Disciplined gold-standard; Act-2 elite kill. |
| 7 | 28.1 | run15 gpt-5.5 DEFECT stall | stall | 0 | 3 | 19 | Reached Act 3 then stalled; act bonus carries it. |
| 16 | 24.6 | run22 claude-opus-4.7 SILENT | death | 16 | 1 | 14 | Act-1 boss death; error penalty drops it below the F17 cluster. |
| 22 | 16.5 | run03 glm-5.1 REGENT | death | 7 | 1 | 5 | Earliest death (Nibbits F7). |

Range: 16.5 – 79.4 (62.9 spread); 22/22 unique scores; per-character medians 24.6–33.8; per-model means 22.0–41.7. Rank ordering matches the qualitative ranking in `trial-v0-findings-audit.md`. Weights are **frozen** at the values above for v1.

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

**`priors-<CHARACTER>.md` files are frozen with this protocol.** Editing any of them mid-trial invalidates all prior B0 runs of the affected character.

---

## Open items before v1 freeze

These must be resolved before kickoff:

1. **Pick the three seeds.** Operator-side; needs one Opus 4.7 control pass per character to confirm diversity.
2. **Write `priors-<CHARACTER>.md` (×5).** Source: v0 audit §4 + Opus's behavioral patterns. One file per character (`priors-IRONCLAD.md`, `priors-SILENT.md`, `priors-DEFECT.md`, `priors-REGENT.md`, `priors-NECROBINDER.md`); each ~3–5 pages with character-neutral fundamentals + the assigned character's notes inlined. Rules 1–6 universal across all five files; Rule 2 and Rule 4 lightly tuned per character; the closing "Character notes" section is unique per file.
3. **SpireBridge v0.2.0.** Implement character-resource completeness (Stars / Orbs / Osty-as-ally at stable combat-root paths). (Done in v0.2.0: `stateVersion` field on every snapshot, stale `modVersion` literal replaced with `MainFile.BridgeVersion`, startup version log line, `ForceRefresh` IPC verb, `state_inconsistent` event detection (`available[]` empty on non-boss floor for two consecutive ticks), `SPIREBRIDGE_IPC_DIR` rename with `HERMES_IPC_DIR` deprecated alias, `tools/preflight-dll-version.ps1` operator script. `StartRun.seed` already forwarded correctly since v0.1.5; `CancelTargeting` deliberately omitted, see Bridge change #8.)
4. **Composite score weights — RESOLVED.** Calibrated against v0 data via `tools/calibrate-composite.py` (n=21). Proposed weights produce the expected ranking: deepest run #1 (run11 79.4), Act-1 deaths cluster 24–27, Opus discipline run scores below deeper-floor runs by design (the formula rewards depth, not discipline). 21/21 unique scores; no ties. Weights frozen — see §Scoring calibration table above.
5. **Run-cap bump confirmation.** Default 500 → 1000 in bridge; operator-side cap also raised.
6. **`tools/preflight-dll-version.ps1`.** New helper. Verifies (a) `modVersion` matches repo HEAD, (b) bridge build is ≥v0.2.0, (c) the active IPC root reported in `trace.log` matches the operator-intended `SPIREBRIDGE_IPC_DIR` / `HERMES_IPC_DIR` for this run (multi-instance safety).
7. **Tool-leakage audit.** Add `preflight_screen` column to runs.csv to track per-run pre-flight state.
8. **Necrobinder per-character resource — RESOLVED.** No separate player-side meter exists. Osty (in `combat.allies[]`) is the entire mechanic: Osty soaks unblocked damage before the player does. Documented in `priors-NECROBINDER.md` Rule 2 and the Necrobinder character paragraph; no v0.2.0 bridge work needed beyond the already-spec'd ally rendering.
9. **Update Python port spec to match.** `docs/port-autopilot-lib-python.md` must reflect the SpireBridge rename, env-var alias policy, and multi-instance test plan; update before port begins.

---

Source documents:
- `docs/benchmark/protocol.md` — trial-v0 (predecessor).
- `docs/benchmark/trial-v0-findings-audit.md` — empirical justification for every v1 delta above.
- `docs/benchmark/agent-prompt-v0.md` — trial-v0 run preamble (frozen, archival).
- `docs/benchmark/agent-prompt-v1-A0.md` / `agent-prompt-v1-B0.md` — trial-v1 run preambles (zero-shot / with priors).
