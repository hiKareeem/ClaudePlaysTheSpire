# Trial-v0 Findings Audit

Synthesis of all 21 run records (runs01–run21) from `docs/benchmark/runs/`.
Purpose: feed v1 spec design and Python-port scoping decisions.

Scope: HermesBridge v0.1.1–v0.1.5, A0-zero-shot arm only, 5 characters × 5 models.

## 1. Headline numbers

- Runs recorded: 21 (runs.csv has 18; runs09, runs10, runs21 not yet appended)
- Halt reasons: death=17, stall=3, manual_halt=1
- Best run: run11 gpt-5.5-ironclad — Act 3, F50, killed by Test Subject #C14 (Act 3 boss). 6h wall, 187M tokens.
- Deepest deepseek: run19 necrobinder F23, run20 defect F22 (both combat_misplay).
- Stalled runs: run08 (gemini-regent, Act 2 F19, abandoned mid-run), run15 (gpt-defect, Act 3 F41, IsTravelEnabled desync), run17 (deepseek-silent, F10, post-treasure 0-available).
- Failed run: run18 (stale Steam DLL = modVersion 0.1.1, pre-AtomicFileWriter fix).

## 2. Bridge bugs surfaced

### Fixed (verified in v0.1.4/v0.1.5)
- **BKI-001 — Merchant treasure-skip routing**
  - Symptom: travel to merchant after treasure room caused silent state corruption.
  - Fix: BridgeCommandDispatcher.cs ~L1019–1140. Released v0.1.4. Verified runs ≥17.
- **BKI-002 — IsTravelable race vs. MapPointState**
  - Symptom: agent receives `is_travelable=true` for nodes the game rejects on travel.
  - Fix: validator OR (BridgeCommandDispatcher.cs ~L1260) + state-extractor branch (BridgeStateExtractor.cs ~L2350). Released v0.1.4.

### Open / partially-mitigated
- **IPC schema desync: IsTravelEnabled vs. MapPointState**
  - Still observed in run15 (Act 3 F41) and run17 (post-treasure F10): map shows 0 available nodes although a route exists.
  - Hypothesis: MapPointState sometimes lags by one tick after treasure/event resolution. BKI-002 fix narrowed but did not close the window.
  - Action for v1: add a server-side retry/refresh on `get_map_state` when available_count==0 and HUD says floor not boss.
- **AtomicFileWriter stale-temp (run18 trigger)**
  - Source fixed; deployment lag caused run18 to load v0.1.1 DLL from Steam mods folder.
  - Action: pre-flight check that compares deployed DLL `modVersion` to repo HEAD; abort run if stale.

### Diagnostic / observability gaps
- BridgePaths static-init re-entrancy was only caught after refactor + 14 tests. Add test coverage for: bad instance-id chars, BOM, `..`, slashes, control chars (already done), and APPDATA missing/locked (not yet covered).
- No structured event for "map state inconsistent" — agent only sees the 0-available list and stalls.

## 3. IPC / protocol findings

- **Error counts per run**: range 1–25. Correlation is with run length and depth, not model skill.
  - High-error runs: run15 (gpt-defect, 19), run19 (deepseek-necrobinder, 18), run20 (deepseek-defect, 25), run11 (gpt-ironclad, 14).
  - Low-error runs are universally short.
- **Most common errors**: invalid hand-index after card play, travel-to-non-adjacent node, action-after-combat-end.
- **Hand-index drift**: agent picks slot N, plays it, then references slot N expecting a different card. Recorded explicitly in run02 (Silent boss). Suggests v1 should require the agent to re-read state after every play; or the schema should suppress stale indices.
- **Action-after-combat-end**: agent issues `play_card` after `end_turn` resolves the encounter. Bridge returns error; ideally schema should expose a transition state.

## 4. Agent-side patterns (model-agnostic)

- **Combat misplay (~7 deaths)**: card-order errors, ignoring buff/debuff stacks, mis-targeting AoE.
- **Boss under-prepped (~4)**: reaching boss without block density (Ironclad runs), no scaling answer (Silent vs. Kin Priest), no AoE (Defect vs. Vantom).
- **Act-1 boss kills (~4)**: Kin Priest, Vantom recurrent. Skill-floor gate.
- **Elite under-respect**: Bygone Effigy, Hunter Killer, Decimillipede, Test Subject C14 — all Str-scaling or burst threats.
- **Map routing**: only one explicit fail (run01). Map literacy is generally fine.
- **Event combat loss**: run04 — agent took a combat-event option without reading the threat.
- **Potion misclick**: run07 — used wrong potion at wrong moment.

## 5. PowerShell / operator-side surface

Minimal direct mentions in run records. The pain that *did* show up:
- finalize-run idempotency (now fixed, ea050d5).
- BOM in run summaries (now fixed).
- Stale Steam-mods DLL (run18) — operator process, not script.
- No record explicitly blames PowerShell language quirks. The scripts work; they're just not portable.

Implication for Python-port decision: the PowerShell glue is **not the bottleneck**. Port for portability/contributor reach, not for reliability.

## 6. Cost / time envelope

- Wall time per run: 30 min – 6 h.
- Cost per run (deepseek-v4-pro): $8–20.
- Token usage extreme outlier: run11 187M (gpt-5.5, longest run).
- Context utilization: routinely <80%; budget cap was never the halt reason in v0.

## 7. Halt-reason taxonomy gaps

Current halts: death, stall, manual_halt, victory (unobserved in v0).
Proposed v1 additions:
- `rate_limit` — distinct from generic error.
- `error_streak` — N consecutive bridge errors (e.g. 5).
- `state_inconsistent` — bridge reports impossible state (0 available, not boss).
- `budget_exhausted` — explicit cap hit (time, tokens, or $).

## 8. Implications for v1 spec

1. **Keep A0-zero-shot as control**; add **B0-with-priors** arm using a short hand-curated rules sheet (block density, elite buff awareness, boss-prep checklist). Hypothesis: closes ~half of combat-misplay deaths.
2. **k=3 per cell** (model × character) — current k=1 is too noisy for the spread observed (run10 vs. run11 same model differ by 30+ floors).
3. **Ascensions: stay at A0/A1 for v1**; revisit when win rate on A0 > 20%.
4. **Composite score**: floors_reached + 10·act_reached + 50·victory − 0.1·errors. Rewards depth, penalizes IPC noise.
5. **Halt taxonomy**: implement the 4 new reasons above.
6. **Pre-flight checks**: deployed DLL modVersion match, IPC dir writable, hermes-instance.cfg sane.
7. **Stall detection**: bridge-side, on 0-available + not-boss + 2 ticks no progress → emit `state_inconsistent` and let agent retry once before halting.

## 9. Implications for Python port

- **Do not port**: HermesBridgeCode/*.cs (Godot/Mono/Harmony — pinned to game runtime).
- **Worth porting**: `tools/*.ps1`, `tools/maintainer/*.ps1`, `autopilot-lib.ps1`. Reasons: contributor reach, cross-platform (Linux Steam Proton users), test-ability via pytest.
- **Order**: `spirebench-summary.py` already Python — proves the model. Next candidates by ROI: `finalize-run.ps1` (idempotent, well-scoped), then `autopilot-lib.ps1` (largest, IPC-heavy).
- **Defer**: maintainer scripts that Kareem alone runs (release packaging, Nexus push). Low contributor value.

## 10. Open questions for v1 design session

- Do we expose a `state_version` int on every bridge response so the agent can detect staleness?
- Do we add a `force_refresh` IPC verb, or have the bridge auto-refresh on inconsistency?
- Composite score weights — calibrate on v0 data or pick a priori?
- B0 priors: shipped as system prompt addendum, or as a tool the agent can `read_priors` against on demand?
- Run-cap default: bridge currently 500, plan says 1000. Confirm before v1 kickoff.

---

Source: 21 run records under `docs/benchmark/runs/run01..run21`, `docs/benchmark/runs.csv`, `docs/bridge-known-issues.md`.
Generated as input to v1 spec + Python-port scoping decisions.

---

## Addendum: runs 22–25 (claude-opus-4.7 cohort)

This addendum extends the trial-v0 audit to the final cohort of runs (claude-opus-4.7
across all five characters: ironclad-run21 already covered above, plus silent-run22,
regent-run23, necrobinder-run24, defect-run25). Total trial-v0 corpus is now n=25.

### A1. Updated headline numbers (n=25)

After regenerating `tools/spirebench-summary.py` against the full 25-run corpus
(and fixing a `final_floor` resolver bug — see A6 below):

- Halt reasons: death=22, stall=3.
- Best run: **run11 gpt-5.5-ironclad — F50, Act 3 boss (Test Subject #C14)**. Unchanged.
- Second-deepest: **run25 claude-opus-4.7-defect — F33, Act 2 boss (Knowledge Demon)**.
  Died T3 with HP 2 → 0 from Frost-orb-passive block timing miscalculation.
- Per-model max-floor ranking: gpt-5.5 (50) > claude-opus-4.7 (33) > deepseek-v4-pro (23)
  > gemini-3.1-pro-preview (19) > glm-5.1 (17).
- Per-model median: gpt-5.5 (28) > claude-opus-4.7 (23) > deepseek-v4-pro (20)
  > gemini-3.1-pro-preview (17) > glm-5.1 (15).
- Zero victories. The skill ceiling for A0-zero-shot frontier LLMs on StS2 sits
  somewhere between the Act 2 boss and the Act 3 boss.

### A2. Bridge findings (new in v22–v25)

These are observations the claude cohort surfaced that the earlier 21-run audit
did not catalogue. Most relate to potion targeting, reward indexing, and EOT
state-read ordering.

- **`UsePotion` `AnyPlayer` self-buff potions show 1–2 ticks of read lag** (run24 F22
  Heart of Iron → Plating; same pattern as earlier potion observations at
  run17/run21). `ok=true` returns immediately, slot consumes, but
  `combat.player.powers` is empty on the next `read-combat`. Issuing one cheap
  card play (or even just another state read) bridges the lag. Workaround is
  cheap; the underlying issue is that `UsePotion`'s atomic write resolves before
  the power-application hook runs. **Action for v0.2.x**: bind power-application
  into the same `RequestWrite` cycle as the potion command, so the power is
  visible on the very next read.
- **`Gambler's Brew` ignores `cardHandIndices`** (run24 F20). The parameter is
  accepted (`ok=true`) but the redraw modal opens regardless. Worst-of-both:
  caller has to handle the modal anyway. **Action**: either honor
  `cardHandIndices` and skip the modal, or remove it from the schema.
- **`SelectReward rewardPosition` indexes against the live (mutating)
  `state.rewards` array** (run24 F20). After consuming a Gold reward at
  position 0, position 1 became position 0; the bridge silently re-mapped, but
  the agent's mental model expected stable IDs. **Action**: expose the stable
  `position` field that's already in the payload but currently ignored on
  read-back; or document the live-index rule explicitly in `SKILL.md`.
- **`SelectCardsInGrid` parameter is `cardIndices`** (run24 F21 shop card
  removal) — not `indices` or `Indexes`. The earlier audit didn't capture this
  because no run before 24 had used grid-select. Now in `SKILL.md`.
- **`ShopPurchasePotion` is invalid; use `Purchase category='potion'`** (run24
  F21). Surface area cleanup item.
- **`CancelTargeting` is unrecognised in v0.1.5** (`unknown command type`).
  Already on the v0.2.0 backlog; deferred per session decision.
- **EOT block-from-orb-passive read ordering** (run25 F33 Knowledge Demon T3 —
  the run-killing observation). On the player's own turn, `combat.player.Block`
  reads `0` while Frost-orb passive block (`passive:7` per orb) is queued for
  the EndTurn resolution. The agent computed projected post-EOT block from the
  orb count and Disintegration self-damage tick; the projection was off by
  one passive trigger. The bridge state model exposes orb metadata but does not
  expose **pending EOT effects**. **Action for v1**: add a
  `combat.pendingEndOfTurn` block listing scheduled passives, EoT damage, and
  status decay; this turns "agent guesses the math" into "agent reads the math".

### A3. Agent-side patterns (new observations)

The claude cohort provided four more data points for the existing failure-mode
taxonomy:

- **run22 silent F16 (The Kin boss, T9)**: Race-the-Priest plan was correct;
  Frail/Weak debuff cycle starved block economy. HP 6/70 at death with 28 dmg
  incoming and 19 max block. Pattern: **Act-1-boss-under-block** (already
  documented; recurrent).
- **run23 regent F17 (Vantom, lethal misplay by 5 HP)**: Played GS → PC+ → Strike
  on the lethal turn instead of GS → PC+ to free draw-2 fuel for a third
  attacker. Vantom died at HP 5 over Regent's HP 0. **New pattern**: lethal-line
  arithmetic mistake in a winnable boss state. The agent had the cards; the
  ordering was wrong by one play. Recommended seed `12343810909327937521` as a
  regression fixture for v1's combat-line evaluator.
- **run24 necrobinder F23 (Spiny Toad Act 2 normal monster)**: Cleared Vantom
  Act 1 boss with Doom-stack (HP 35/66 post-boss). Died on a Thorns-x5 +
  Spike-Explosion combo by a 9-HP gap. Pattern: **Thorns under-respect**
  (similar to elite under-respect in §4 of the main audit; new sub-pattern is
  *normal-monster* under-respect during Act-2 ramp). Self-identified
  better play: skip Severance T2, lean entirely on Plating + Doom decay.
- **run25 defect F33 (Knowledge Demon Act 2 boss)**: Deepest claude run.
  T3 plan was sound (Hotfix echo for Focus, Focused Strike + Sunder + Strength
  Potion + Metamorphosis); died because the Frost-orb-passive block resolution
  produced 0 block at start of enemy turn instead of the projected 14. **New
  pattern**: bridge-imposed *epistemic* failure — the agent had the right plan
  for the math it could see, but the bridge didn't expose enough state to know
  the actual EOT order. This is the strongest argument so far for the
  `pendingEndOfTurn` schema addition.

### A4. Cost / time envelope (updated)

claude-opus-4.7 cost ($0 reported because OpenCode's claude-opus-4.7 channel
runs through a flat-fee provider during this trial; not directly comparable
to the deepseek $8–$20/run figure):

| run    | wall (s) | tokens_total | step_finish |
| ------ | -------- | ------------ | ----------- |
| run21  |  ~17000  | ~73M         | ~750        |
| run22  |   5530   | 51M          | 410         |
| run23  | 11340    | 62M          | 470         |
| run24  |  7342    | 43M          | 585         |
| run25  | 50289    | 104M         | 1250        |

Run25 dwarfs the cohort: 14h wall, 104M tokens, 1250 step-finishes, for a F33
death. This sets a useful trial-v1 budget reference: a serious Act-2-boss attempt
with a verbose-thinking model can take 12+ hours and 100M+ tokens. The composite
score (floors + 10·act + 50·victory − 0.1·errors) on this run is
33 + 20 + 0 − 0 = 53, behind run11's 50 + 30 + 0 − 1.4 = 78.6.

### A5. Items resolved since the original audit

The original audit (compiled at run21) listed these as open. Status as of
v0.2.0 (commit `35cb620`):

- ✅ **`stateVersion` int on every bridge response** — landed in v0.2.0. Agents
  can now detect staleness directly.
- ✅ **`ForceRefresh` IPC verb** — landed in v0.2.0. Bridge auto-refresh on
  inconsistency is still desirable but the explicit verb closes the immediate
  gap.
- ✅ **`state_inconsistent` halt-reason event** — landed in v0.2.0. Agents see
  it; runs 22–25 did not trigger it (no IsTravelEnabled desync observed).
- ✅ **Pre-flight DLL-version check** (`tools/preflight-dll-version.ps1`) —
  landed in v0.2.0. Catches the run18 stale-Steam-DLL failure mode before
  the run starts.
- ✅ **Multi-instance support** (`SPIREBRIDGE_IPC_DIR` primary,
  `HERMES_IPC_DIR` deprecated alias) — landed in v0.2.0.
- ✅ **Boss identity in `state.Act.BossEncounter.Id`** (also `SecondBossEncounter.Id`
  for treasure-room previews) — landed in v0.2.0. `read-map.ps1` `_PrettyBossId`
  helper renders `THE_KIN_BOSS → "The Kin Boss"`.
- 🟡 **`rate_limit` / `error_streak` / `budget_exhausted` halt reasons** —
  unchanged from the v1 spec backlog.
- 🟡 **k=3 per cell** — unchanged; trial-v1 will run 5×5×k=3×2 arms = 150 runs.
- 🟡 **B0-priors arm** — unchanged; trial-v1 design.
- 🟡 **IsTravelEnabled vs MapPointState desync** (the run15/run17 stalls) —
  no new occurrences in runs 22–25, but no proactive fix either. Likely needs
  the bridge-side `state_inconsistent` retry loop sketched in §2 of the main
  audit.

### A6. Summary-script bug (fixed mid-audit)

`tools/spirebench-summary.py`'s `final_floor()` function read `death_floor`
as the cumulative depth. This was correct for the original 21 runs (whose
`finalize-run.ps1` wrote cumulative `death_floor`) but wrong for the claude
cohort: `finalize-run.ps1` was updated mid-trial to write **in-act** `death_floor`
(resets each Act), and the corresponding cumulative depth lives in `total_floors`.

Without the fix, claude-opus-4.7 reported `max_floor=23` (run24) and was
ranked behind deepseek-v4-pro despite run25 actually reaching F33. The
heatmap also under-reported claude deaths' floor positions.

Resolved by changing `final_floor()` to return `max(death_floor, total_floors)`
when both are present. Both fields are nominally cumulative; the `max`
defensively rescues the in-act-encoded records without breaking the
cumulative-encoded ones (where `death_floor ≥ total_floors` because the
death floor is *entered* but not *completed*). Also updated
`chart_death_heatmap` to use `final_floor` instead of raw `death_floor`.

**Action for v1**: pick one encoding for `death_floor` (cumulative) and have
`finalize-run.ps1` enforce it. Ideally remove `total_floors` once the encoding
is canonical, since it carries the same information.

### A7. Implications for v1 (refinements)

The original audit's v1 implications stand. Refinements based on runs 22–25:

- **Add `combat.pendingEndOfTurn` to the state schema** (run25 root cause).
  Frost-orb passives, Disintegration self-damage, status decay, scheduled
  damage — all currently invisible to the agent. This is the single highest-ROI
  bridge-side improvement for v1.
- **Treat lethal-line arithmetic as a v1 failure-mode taxonomy entry**
  (run23 root cause). Frontier models can hold the boss math but mis-order
  the lethal line; this is distinct from "boss under-prepped" and worth its
  own composite-score sub-bucket if we want to measure it.
- **Boss-identity helper is shipped** (`_PrettyBossId`); v1 spec should
  require its use in run records so the `killed_by` and `boss_reached` fields
  read consistently across the corpus.
- **Run budget guidance for v1**: based on run25, expect Act-2-boss-class
  attempts to consume 12+ hours wall and 100M+ tokens. The 1000-step run-cap
  is fine; the 24-hour soft wall-clock cap should remain. No `budget_exhausted`
  halts triggered in trial-v0.
