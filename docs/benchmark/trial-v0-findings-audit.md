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
